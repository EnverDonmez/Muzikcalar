package com.muzikcalar.app;

import android.content.SharedPreferences;
import android.util.Log;
import androidx.media3.common.MediaItem;
import androidx.media3.common.Player;
import androidx.media3.common.Timeline;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.exoplayer.ExoPlayer;
import androidx.media3.exoplayer.source.ShuffleOrder;
import java.util.ArrayList;
import org.json.JSONArray;

@UnstableApi
final class PlaybackSessionStore {
    private final SharedPreferences preferences;

    PlaybackSessionStore(SharedPreferences preferences) {
        this.preferences = preferences;
    }

    void save(Player player, boolean includeQueue){
        try{
            SharedPreferences.Editor edit=preferences.edit();
            if(includeQueue){
                JSONArray tracks=new JSONArray();
                for(int i=0;i<player.getMediaItemCount();i++)tracks.put(Track.fromMedia(player.getMediaItemAt(i)).json());
                edit.putString("queue",tracks.toString());
                JSONArray shuffle=new JSONArray();
                for(int index:shuffleOrder(player))shuffle.put(index);
                edit.putString("shuffleOrder",shuffle.toString());
            }
            edit.putInt("index",player.getCurrentMediaItemIndex()).putLong("position",player.getCurrentPosition())
                    .putInt("repeat",player.getRepeatMode()).putBoolean("shuffle",player.getShuffleModeEnabled())
                    .putFloat("volume",player.getVolume()).apply();
        }catch(RuntimeException e){Log.w("Muzikcalar","Oturum kaydedilemedi",e);}
    }
    void restore(ExoPlayer player){
        try{
            ArrayList<MediaItem> items=new ArrayList<>();
            for(Track track:Track.list(new JSONArray(preferences.getString("queue","[]"))))items.add(track.media());
            if(!items.isEmpty())player.setMediaItems(items,Math.max(0,Math.min(preferences.getInt("index",0),items.size()-1)),
                    Math.max(0,preferences.getLong("position",0)));
            JSONArray savedOrder=new JSONArray(preferences.getString("shuffleOrder","[]"));
            if(!items.isEmpty()&&savedOrder.length()==items.size()){
                int[] order=new int[savedOrder.length()];
                boolean[] seen=new boolean[order.length];
                boolean valid=true;
                for(int i=0;i<order.length;i++){
                    int index=savedOrder.optInt(i,-1);
                    if(index<0||index>=order.length||seen[index]){valid=false;break;}
                    seen[index]=true;order[i]=index;
                }
                if(valid)player.setShuffleOrder(new ShuffleOrder.DefaultShuffleOrder(order,System.nanoTime()));
            }
            player.setRepeatMode(preferences.getInt("repeat",Player.REPEAT_MODE_OFF));
            player.setShuffleModeEnabled(preferences.getBoolean("shuffle",false));
            player.setVolume(preferences.getFloat("volume",.8f));
        }catch(Exception e){Log.w("Muzikcalar","Oturum geri yüklenemedi",e);}
    }

    static int[] shuffleOrder(Player player){
        Timeline timeline=player.getCurrentTimeline();
        int[] order=new int[timeline.getWindowCount()];
        int index=timeline.getFirstWindowIndex(true);
        for(int i=0;i<order.length;i++){
            order[i]=index;
            index=timeline.getNextWindowIndex(index,Player.REPEAT_MODE_OFF,true);
        }
        return order;
    }

}
