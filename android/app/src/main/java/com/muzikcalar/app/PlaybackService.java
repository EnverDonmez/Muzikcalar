package com.muzikcalar.app;

import android.app.PendingIntent;
import android.content.Intent;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import androidx.annotation.Nullable;
import androidx.media3.common.*;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.datasource.*;
import androidx.media3.exoplayer.ExoPlayer;
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory;
import androidx.media3.exoplayer.source.ShuffleOrder;
import androidx.media3.session.*;
import com.google.common.util.concurrent.Futures;
import com.google.common.util.concurrent.ListenableFuture;
import org.json.JSONObject;

@UnstableApi
public final class PlaybackService extends MediaSessionService {
    static final SessionCommand QUEUE_TRACK=new SessionCommand("com.muzikcalar.app.QUEUE_TRACK",Bundle.EMPTY);
    static final int PLAY_NOW=0, PLAY_NEXT=1, APPEND=2, SKIP_NEXT=3;
    private ExoPlayer player;
    private MediaSession session;
    private MusicApp app;
    private PlaybackSessionStore sessionStore;
    private ConnectivityManager connectivity;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private int generation, attempts;
    private long attemptPosition;
    private boolean restoring, networkRegistered;
    private Runnable waitingForNetwork;
    private final ConnectivityManager.NetworkCallback networkCallback=new ConnectivityManager.NetworkCallback(){
        @Override public void onAvailable(Network network){handler.post(()->{
            Runnable retry=waitingForNetwork;
            waitingForNetwork=null;
            if(retry!=null)handler.postDelayed(retry,750);
        });}
    };

    @Override public void onCreate(){
        super.onCreate();
        app=MusicApp.instance;
        sessionStore=new PlaybackSessionStore(getSharedPreferences("playback",0));
        connectivity=getSystemService(ConnectivityManager.class);
        DefaultHttpDataSource.Factory http=new DefaultHttpDataSource.Factory().setUserAgent(Youtube.AGENT)
                .setConnectTimeoutMs(15000).setReadTimeoutMs(20000);
        ResolvingDataSource.Factory sources=new ResolvingDataSource.Factory(new DefaultDataSource.Factory(this,http),spec->{
            if(!"muzikcalar".equals(spec.uri.getScheme()))return spec;
            if(connectivity!=null&&connectivity.getActiveNetwork()==null)
                throw new Youtube.SourceFailure("Ağ bağlantısı yok.",true,null);
            Youtube.Stream stream=app.youtube.resolve(spec.uri.getLastPathSegment());
            return spec.withUri(Uri.parse(stream.url)).withAdditionalHeaders(stream.headers);
        });
        player=new ExoPlayer.Builder(this).setMediaSourceFactory(new DefaultMediaSourceFactory(sources))
                .setAudioAttributes(new AudioAttributes.Builder().setUsage(C.USAGE_MEDIA)
                        .setContentType(C.AUDIO_CONTENT_TYPE_MUSIC).build(),true)
                .setHandleAudioBecomingNoisy(true).setWakeMode(C.WAKE_MODE_NETWORK).build();
        PendingIntent open=PendingIntent.getActivity(this,0,new Intent(this,MainActivity.class),
                PendingIntent.FLAG_IMMUTABLE|PendingIntent.FLAG_UPDATE_CURRENT);
        session=new MediaSession.Builder(this,player).setSessionActivity(open).setCallback(new MediaSession.Callback(){
            @Override public MediaSession.ConnectionResult onConnect(MediaSession session,MediaSession.ControllerInfo controller){
                if(controller.getUid()!=android.os.Process.myUid())return MediaSession.Callback.super.onConnect(session,controller);
                return new MediaSession.ConnectionResult.AcceptedResultBuilder(session)
                        .setAvailableSessionCommands(MediaSession.ConnectionResult.DEFAULT_SESSION_COMMANDS.buildUpon()
                                .add(QUEUE_TRACK).build()).build();
            }
            @Override public ListenableFuture<SessionResult> onCustomCommand(MediaSession session,
                    MediaSession.ControllerInfo controller,SessionCommand command,Bundle args){
                if(!QUEUE_TRACK.equals(command)||controller.getUid()!=android.os.Process.myUid())
                    return Futures.immediateFuture(new SessionResult(SessionError.ERROR_PERMISSION_DENIED));
                try{
                    int mode=args.getInt("mode",-1);
                    if(mode==SKIP_NEXT){
                        int next=player.getNextMediaItemIndex();
                        if(next==C.INDEX_UNSET)return Futures.immediateFuture(new SessionResult(SessionError.ERROR_INVALID_STATE));
                        startQueueIndex(next);
                        return Futures.immediateFuture(new SessionResult(SessionResult.RESULT_SUCCESS));
                    }
                    if(mode<PLAY_NOW||mode>APPEND)throw new IllegalArgumentException();
                    queueTrack(Track.from(new JSONObject(args.getString("track",""))),mode);
                    return Futures.immediateFuture(new SessionResult(SessionResult.RESULT_SUCCESS));
                }catch(Exception e){
                    Log.w("Muzikcalar","Sıra düzenlenemedi",e);
                    return Futures.immediateFuture(new SessionResult(SessionError.ERROR_BAD_VALUE));
                }
            }
        }).build();
        player.addListener(new Player.Listener(){
            @Override public void onEvents(Player p,Player.Events events){
                if(restoring)return;
                if(events.contains(Player.EVENT_TIMELINE_CHANGED)||events.contains(Player.EVENT_MEDIA_ITEM_TRANSITION)
                        ||events.contains(Player.EVENT_REPEAT_MODE_CHANGED)||events.contains(Player.EVENT_SHUFFLE_MODE_ENABLED_CHANGED)
                        ||events.contains(Player.EVENT_VOLUME_CHANGED)||events.contains(Player.EVENT_IS_PLAYING_CHANGED)
                        ||events.contains(Player.EVENT_POSITION_DISCONTINUITY))sessionStore.save(p,events.contains(Player.EVENT_TIMELINE_CHANGED));
                if(p.isPlaying()&&(events.contains(Player.EVENT_IS_PLAYING_CHANGED)||events.contains(Player.EVENT_MEDIA_ITEM_TRANSITION))){
                    app.recoveryStatus="";
                    MediaItem item=p.getCurrentMediaItem();
                    if(item!=null)app.record(Track.fromMedia(item));
                }
            }
            @Override public void onMediaItemTransition(@Nullable MediaItem item,int reason){
                generation++;attempts=0;attemptPosition=0;waitingForNetwork=null;app.recoveryStatus="";
            }
            @Override public void onPlayWhenReadyChanged(boolean ready,int reason){
                if(!ready){generation++;waitingForNetwork=null;app.recoveryStatus="";}
            }
            @Override public void onPlayerError(PlaybackException error){recover(error);}
        });
        restoring=true;
        try { sessionStore.restore(player); }
        finally { restoring=false; }
        if(connectivity!=null){
            try{connectivity.registerDefaultNetworkCallback(networkCallback);networkRegistered=true;}
            catch(RuntimeException e){Log.w("Muzikcalar","Ağ izleyicisi başlatılamadı",e);}
        }
        handler.postDelayed(checkpoint,5000);
    }

    private void queueTrack(Track track,int mode){
        int count=player.getMediaItemCount(), current=player.getCurrentMediaItemIndex();
        boolean idle=player.getPlaybackState()==Player.STATE_IDLE&&player.getPlayerError()==null
                &&!player.getPlayWhenReady()&&player.getCurrentPosition()==0;
        boolean beforeCurrent=mode!=APPEND&&idle;
        int inserted=mode==APPEND?count:count==0?0:beforeCurrent?current:current+1;
        int[] order=PlaybackSessionStore.shuffleOrder(player);
        player.addMediaItem(inserted,track.media());
        // Keep explicit queue choices when shuffle is enabled just before or after this edit.
        int[] next=insertShuffleEntry(order,current,inserted,mode==APPEND,beforeCurrent);
        player.setShuffleOrder(new ShuffleOrder.DefaultShuffleOrder(next,System.nanoTime()));
        if(mode==PLAY_NOW)startQueueIndex(inserted);
        else if(beforeCurrent&&count>0){
            player.seekToDefaultPosition(inserted);
        }
        sessionStore.save(player,true);
    }
    private void startQueueIndex(int index){
        player.seekToDefaultPosition(index);
        if(player.getPlaybackState()==Player.STATE_IDLE||player.getPlayerError()!=null)player.prepare();
        player.play();
    }

    static int[] insertShuffleEntry(int[] old,int current,int inserted,boolean append,boolean beforeCurrent){
        int slot=old.length;
        if(!append)for(int i=0;i<old.length;i++){
            if(old[i]==current){slot=i+(beforeCurrent?0:1);break;}
        }
        int[] updated=new int[old.length+1];
        int target=0;
        for(int index:old){
            if(target==slot)updated[target++]=inserted;
            updated[target++]=index>=inserted?index+1:index;
        }
        if(target==slot)updated[target]=inserted;
        return updated;
    }

    private void recover(PlaybackException error){
        MediaItem item=player.getCurrentMediaItem();
        if(item==null)return;
        Track track=Track.fromMedia(item);
        if(!track.isLocal())app.youtube.invalidate(track.id);
        boolean transientFailure=!track.isLocal()&&PlaybackRecovery.shouldRetry(error);
        app.recoveryStatus="";
        Log.w("Muzikcalar","Playback error: "+error.getErrorCodeName()
                +" retryable="+transientFailure+" playingRequested="+player.getPlayWhenReady()+" attempts="+attempts);
        if(!transientFailure||!player.getPlayWhenReady()||attempts>=2)return;
        app.youtube.verifyNextResolve(track.id);
        int version=generation, index=player.getCurrentMediaItemIndex();
        long position=player.getCurrentPosition();
        Runnable retry=()->{
            if(player==null||version!=generation||player.getCurrentMediaItemIndex()!=index||!player.getPlayWhenReady())return;
            app.recoveryStatus="Yeniden bağlanıyor…";
            attempts++;
            attemptPosition=position;
            player.seekTo(index,position);
            player.prepare();
        };
        if(networkRegistered&&connectivity.getActiveNetwork()==null){
            app.recoveryStatus="Bağlantı bekleniyor…";
            waitingForNetwork=retry;
        }else{
            app.recoveryStatus="Yeniden bağlanıyor…";
            handler.postDelayed(retry,attempts==0?750:2000);
        }
    }
    private final Runnable checkpoint=new Runnable(){
        public void run(){
            if(player==null)return;
            if(player.isPlaying()){
                app.recoveryStatus="";
                if(player.getCurrentPosition()>=attemptPosition+10000)attempts=0;
                sessionStore.save(player,false);
            }
            handler.postDelayed(this,5000);
        }
    };
    @Override public MediaSession onGetSession(MediaSession.ControllerInfo controller){return session;}
    @Override public void onDestroy(){
        if(networkRegistered)connectivity.unregisterNetworkCallback(networkCallback);
        handler.removeCallbacksAndMessages(null);
        waitingForNetwork=null;app.recoveryStatus="";
        if(player!=null){sessionStore.save(player,true);player.release();}
        if(session!=null)session.release();
        player=null;
        super.onDestroy();
    }
}
