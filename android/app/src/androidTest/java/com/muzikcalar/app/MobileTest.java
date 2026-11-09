package com.muzikcalar.app;
import android.content.*;
import android.graphics.Bitmap;
import android.net.Uri;
import android.os.*;
import android.provider.MediaStore;
import android.view.*;
import android.view.inputmethod.CompletionInfo;
import android.widget.*;
import androidx.test.core.app.ActivityScenario;
import androidx.test.platform.app.InstrumentationRegistry;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.media3.common.*;
import androidx.media3.common.util.UnstableApi;
import org.junit.*;
import org.junit.runner.RunWith;
import static org.junit.Assert.*;
import java.io.*;
import java.nio.file.*;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.*;
import java.util.concurrent.atomic.AtomicReference;

@UnstableApi @RunWith(AndroidJUnit4.class)
public class MobileTest {
    static void main(Runnable task){InstrumentationRegistry.getInstrumentation().runOnMainSync(task);}
    static void waitFor(java.util.concurrent.Callable<Boolean> condition,long seconds)throws Exception{long until=System.currentTimeMillis()+seconds*1000;while(System.currentTimeMillis()<until){if(condition.call())return;Thread.sleep(250);}fail("Timed out after "+seconds+" seconds");}
    static void shot(String name)throws Exception{Thread.sleep(500);Bitmap b=InstrumentationRegistry.getInstrumentation().getUiAutomation().takeScreenshot();File out=new File(InstrumentationRegistry.getInstrumentation().getTargetContext().getExternalFilesDir(null),name+".png");try(FileOutputStream s=new FileOutputStream(out)){b.compress(Bitmap.CompressFormat.PNG,100,s);}b.recycle();}
    @Test public void libraryPersistsRecoversAndPreservesCorruptFiles()throws Exception{
        Context c=InstrumentationRegistry.getInstrumentation().getTargetContext();File dir=new File(c.getCacheDir(),"library-test-"+UUID.randomUUID());assertTrue(dir.mkdirs());Library lib=new Library(dir);Track t=new Track("n61ULEU7CO0","İçimdeki Şarkı","Çığlık",183,"");Library.Playlist p=lib.create("Gece yürüyüşü");lib.add(p,t);lib.add(p,t);lib.toggle(t);assertEquals(1,p.tracks.size());try{lib.create("GECE YÜRÜYÜŞÜ");fail("duplicate playlist");}catch(IllegalArgumentException expected){}Library read=new Library(dir);assertEquals(t.title,read.favorites.get(0).title);assertEquals(1,read.playlists.get(0).tracks.size());lib.rename(p,"Yeni isim");lib.remove(p,t);assertTrue(new Library(dir).playlists.get(0).tracks.isEmpty());File base=new File(dir,"library.json");Files.copy(base.toPath(),new File(dir,"library.json.bak").toPath());Files.write(base.toPath(),"broken".getBytes());assertEquals("Yeni isim",new Library(dir).playlists.get(0).name);Files.write(base.toPath(),"broken".getBytes());try{new Library(dir);fail("corrupt library");}catch(IOException expected){}assertEquals("broken",new String(Files.readAllBytes(base.toPath())));
    }
    @Test public void interfaceNavigationQueueAndLayouts()throws Exception{
        try(ActivityScenario<MainActivity> scenario=ActivityScenario.launch(MainActivity.class)){
            AtomicReference<MainActivity> ref=new AtomicReference<>();scenario.onActivity(ref::set);MainActivity a=ref.get();waitFor(()->{final boolean[] ready={false};main(()->ready[0]=a.controller!=null);return ready[0];},20);
            main(()->{a.controller.stop();a.controller.clearMediaItems();a.refreshPlayback();a.results.clear();for(int i=0;i<20;i++)a.results.add(new Track("n61ULEU7CO0","Daft Punk – Instant Crush (Official Video) ft. Julian Casablancas","Daft Punk",337,""));a.lastQuery="Daft Punk";a.tab=0;a.showPage(true);});shot("search");
            main(()->{assertTrue(a.list.getHeight()>100);assertEquals(20,a.adapter.getItemCount());assertTrue(a.list.canScrollVertically(1));assertFalse(a.miniPlay.isEnabled());a.list.scrollToPosition(19);});Thread.sleep(400);main(()->assertTrue(a.list.canScrollVertically(-1)));
            Library lib=a.app.library;Library.Playlist p=lib.create("Mobil test listesi");Track t=a.results.get(0);lib.add(p,t);lib.toggle(t);
            main(()->{a.tab=1;a.showPage(true);assertEquals(1,a.adapter.getItemCount());});shot("favorites");
            main(()->{a.tab=2;a.opened=null;a.showPage(true);assertTrue(a.adapter.getItemCount()>0);});shot("playlists");
            main(()->{a.opened=p;a.showPage(true);assertEquals(1,a.adapter.getItemCount());a.addQueue(t);a.addQueue(new Track("dQw4w9WgXcQ","Never Gonna Give You Up","Rick Astley",213,""));a.tab=3;a.opened=null;a.showPage(true);});
            waitFor(()->{final boolean[] ready={false};main(()->ready[0]=a.controller.getMediaItemCount()==2);return ready[0];},10);
            main(()->{a.controller.moveMediaItem(1,0);assertEquals("dQw4w9WgXcQ",a.controller.getMediaItemAt(0).mediaId);a.controller.removeMediaItem(0);assertEquals(1,a.controller.getMediaItemCount());a.showPlayer();});shot("player");
            main(()->{a.playerDialog.dismiss();a.controller.clearMediaItems();a.tab=0;a.showPage(true);});lib.toggle(t);lib.delete(p);
        }
    }
    @Test public void liveSuggestionsReplaceTheQueryAndStartSearch() throws Exception {
        try (ActivityScenario<MainActivity> scenario=ActivityScenario.launch(MainActivity.class)) {
            AtomicReference<MainActivity> ref=new AtomicReference<>();
            scenario.onActivity(ref::set);
            MainActivity activity=ref.get();
            waitFor(()->{
                boolean[] focused={false};
                main(()->focused[0]=activity.hasWindowFocus());
                return focused[0];
            },10);
            main(()->{
                activity.search.requestFocus();
                activity.search.setText("daft punk");
                activity.search.setText("tarkan");
            });
            waitFor(()->{
                boolean[] visible={false};
                main(()->visible[0]=activity.search.isPopupShowing());
                return visible[0];
            },8);
            main(()->{
                AutoCompleteTextView search=activity.search;
                assertEquals(((View)search.getParent()).getId(),search.getDropDownAnchor());
                assertTrue(search.getAdapter().getCount()<=5);
                String selected=search.getAdapter().getItem(0).toString();
                assertTrue(selected.startsWith("tarkan"));
                search.onCommitCompletion(new CompletionInfo(search.getAdapter().getItemId(0),0,selected));
                assertEquals(selected,search.getText().toString());
                assertEquals(selected,activity.lastQuery);
                assertFalse(search.isPopupShowing());
            });
        }
    }
    @Test public void liveSearchAudioPlaybackBackgroundAndControls()throws Exception{
        MusicApp app=(MusicApp)InstrumentationRegistry.getInstrumentation().getTargetContext().getApplicationContext();long start=System.currentTimeMillis();List<Track> found=app.youtube.search("Daft Punk Instant Crush");assertFalse(found.isEmpty());android.util.Log.i("MuzikcalarTest","Live search ms="+(System.currentTimeMillis()-start));start=System.currentTimeMillis();assertFalse(app.youtube.search("Daft Punk Instant Crush").isEmpty());assertTrue(System.currentTimeMillis()-start<500);
        try(ActivityScenario<MainActivity> scenario=ActivityScenario.launch(MainActivity.class)){
            AtomicReference<MainActivity> ref=new AtomicReference<>();scenario.onActivity(ref::set);MainActivity a=ref.get();waitFor(()->{final boolean[] ready={false};main(()->ready[0]=a.controller!=null);return ready[0];},20);
            main(()->{a.controller.setVolume(0);a.playTracks(Collections.singletonList(found.get(0)),0);});
            waitFor(()->{final boolean[] ready={false};main(()->{if(a.controller.getPlayerError()!=null)android.util.Log.e("MuzikcalarTest","Playback",a.controller.getPlayerError());ready[0]=a.controller.getCurrentPosition()>1500&&a.controller.isPlaying();});return ready[0];},120);
            main(()->{a.controller.pause();assertFalse(a.controller.getPlayWhenReady());a.controller.seekTo(15000);a.controller.setRepeatMode(Player.REPEAT_MODE_ONE);assertEquals(Player.REPEAT_MODE_ONE,a.controller.getRepeatMode());a.controller.play();});
            waitFor(()->{final boolean[] ready={false};main(()->ready[0]=a.controller.getCurrentPosition()>16000);return ready[0];},30);
            scenario.moveToState(androidx.lifecycle.Lifecycle.State.CREATED);Thread.sleep(2000);main(()->{assertTrue(a.controller.isPlaying());a.controller.pause();});scenario.moveToState(androidx.lifecycle.Lifecycle.State.RESUMED);main(()->{a.showPlayer();});shot("live-player");main(()->{a.playerDialog.dismiss();a.controller.stop();a.controller.clearMediaItems();});
        }
    }
    @Test public void engineDiagnostic()throws Exception{
        MusicApp app=(MusicApp)InstrumentationRegistry.getInstrumentation().getTargetContext().getApplicationContext();app.youtube.init();android.util.Log.i("MuzikcalarTest","Engine before="+app.youtube.version());app.youtube.update("n61ULEU7CO0");android.util.Log.i("MuzikcalarTest","Engine after="+app.youtube.version());
        Youtube.Stream stream=app.youtube.resolve("n61ULEU7CO0");okhttp3.Request.Builder request=new okhttp3.Request.Builder().url(stream.url).header("Range","bytes=0-1023");for(Map.Entry<String,String> h:stream.headers.entrySet())request.header(h.getKey(),h.getValue());try(okhttp3.Response response=new okhttp3.OkHttpClient().newCall(request.build()).execute()){android.util.Log.i("MuzikcalarTest","Audio probe status="+response.code());assertTrue(response.isSuccessful());}
    }
    @Test public void portableLibraryMergesWindowsRecordsAndKeepsOrder()throws Exception{
        Context context=InstrumentationRegistry.getInstrumentation().getTargetContext();
        File dir=new File(context.getCacheDir(),"portable-test-"+UUID.randomUUID());
        assertTrue(dir.mkdirs());
        Library library=new Library(dir);
        Library.Playlist playlist=library.create("Gece");
        Track first=new Track("n61ULEU7CO0","İlk","Çığlık",42,"");
        Track local=Track.local(Uri.parse("content://media/demo"),"Yerel");
        library.add(playlist,first);library.toggle(local);
        String imported="{\"version\":1,\"favorites\":[{\"id\":\"dQw4w9WgXcQ\",\"title\":\"Yeni\",\"channel\":\"Sanatçı\",\"duration\":61,\"thumbnail\":\"\"}],"
                +"\"playlists\":[{\"id\":\"windows\",\"name\":\"GECE\",\"tracks\":[{\"id\":\"dQw4w9WgXcQ\",\"title\":\"Yeni\",\"channel\":\"Sanatçı\",\"duration\":61,\"thumbnail\":\"\"}]}]}";
        library.importPortable(imported);library.importPortable(imported);
        assertEquals(1,library.playlists.size());
        assertEquals(2,library.playlists.get(0).tracks.size());
        assertEquals("Sanatçı",library.favorites.get(1).artist);
        assertEquals(61,library.favorites.get(1).seconds);
        assertFalse(library.exportPortable().contains("content://"));
        library.move(library.playlists.get(0),first,1);
        assertEquals(first.id,new Library(dir).playlists.get(0).tracks.get(1).id);
        try{library.importPortable("{\"version\":1,\"favorites\":[],\"playlists\":[{}]}");fail("invalid import");}
        catch(org.json.JSONException expected){}
        assertEquals(2,new Library(dir).playlists.get(0).tracks.size());
    }
    @Test public void networkRecoveryStartsAfterReconnect()throws Exception{
        Assume.assumeTrue("ranchu".equals(Build.HARDWARE)
                &&"true".equals(InstrumentationRegistry.getArguments().getString("allowNetworkToggle")));
        Context context=InstrumentationRegistry.getInstrumentation().getTargetContext();
        MusicApp app=(MusicApp)context.getApplicationContext();
        android.net.ConnectivityManager network=context.getSystemService(android.net.ConnectivityManager.class);
        try(ActivityScenario<MainActivity> scenario=ActivityScenario.launch(MainActivity.class)){
            AtomicReference<MainActivity> ref=new AtomicReference<>();scenario.onActivity(ref::set);
            MainActivity activity=ref.get();
            waitFor(()->{final boolean[] ready={false};main(()->ready[0]=activity.controller!=null);return ready[0];},20);
            try{
                shell("svc wifi disable");shell("svc data disable");
                waitFor(()->network.getActiveNetwork()==null,20);
                app.youtube.clearStreams();
                Track track=new Track("n61ULEU7CO0","Bağlantı testi","Müzikçalar",0,"");
                main(()->{activity.controller.setVolume(0);activity.playTracks(Collections.singletonList(track),0);});
                waitFor(()->{final boolean[] waiting={false};main(()->waiting[0]=app.recoveryStatus.startsWith("Bağlantı bekleniyor"));return waiting[0];},60);
                shell("svc wifi enable");shell("svc data enable");
                waitFor(()->{final boolean[] playing={false};main(()->playing[0]=activity.controller.isPlaying()&&activity.controller.getCurrentPosition()>1000);return playing[0];},90);
                main(()->{activity.controller.stop();activity.controller.clearMediaItems();});
            }finally{shell("svc wifi enable");shell("svc data enable");}
        }
    }
    private static void shell(String command)throws IOException{
        ParcelFileDescriptor result=InstrumentationRegistry.getInstrumentation().getUiAutomation().executeShellCommand(command);
        try(InputStream input=new ParcelFileDescriptor.AutoCloseInputStream(result)){
            byte[] buffer=new byte[1024];
            while(input.read(buffer)!=-1){ }
        }
    }
    @Test public void localPlaybackRestoresPausedAndCanSkipBadSource()throws Exception{
        Assume.assumeTrue(Build.VERSION.SDK_INT>=29);
        Context context=InstrumentationRegistry.getInstrumentation().getTargetContext();
        ContentResolver resolver=context.getContentResolver();
        ContentValues values=new ContentValues();
        values.put(MediaStore.Audio.Media.DISPLAY_NAME,"muzikcalar-test-"+UUID.randomUUID()+".wav");
        values.put(MediaStore.Audio.Media.MIME_TYPE,"audio/wav");
        values.put(MediaStore.Audio.Media.RELATIVE_PATH,"Music");
        Uri uri=resolver.insert(MediaStore.Audio.Media.EXTERNAL_CONTENT_URI,values);
        assertNotNull(uri);
        try{
            final int rate=16000,length=rate*2*24;
            ByteBuffer wave=ByteBuffer.allocate(length+44).order(ByteOrder.LITTLE_ENDIAN);
            wave.putInt(0x46464952).putInt(length+36).putInt(0x45564157).putInt(0x20746d66).putInt(16)
                    .putShort((short)1).putShort((short)1).putInt(rate).putInt(rate*2)
                    .putShort((short)2).putShort((short)16).putInt(0x61746164).putInt(length);
            try(OutputStream output=resolver.openOutputStream(uri)){assertNotNull(output);output.write(wave.array());}
            Track playable=Track.local(uri,"Çevrimdışı");
            Track invalid=Track.local(Uri.parse("content://media/external/audio/media/2147483647"),"Eksik dosya");
            context.getSharedPreferences("playback",0).edit().clear()
                    .putString("queue",Track.array(Arrays.asList(playable,invalid,playable)).toString())
                    .putInt("index",0).putLong("position",4000).putBoolean("shuffle",true)
                    .putInt("repeat",Player.REPEAT_MODE_ALL).commit();
            try(ActivityScenario<MainActivity> scenario=ActivityScenario.launch(MainActivity.class)){
                AtomicReference<MainActivity> ref=new AtomicReference<>();scenario.onActivity(ref::set);MainActivity activity=ref.get();
                waitFor(()->{java.util.concurrent.atomic.AtomicBoolean ready=new java.util.concurrent.atomic.AtomicBoolean();main(()->ready.set(activity.controller!=null));return ready.get();},20);
                main(()->{
                    assertEquals(3,activity.controller.getMediaItemCount());assertFalse(activity.controller.getPlayWhenReady());
                    assertEquals(4000,activity.controller.getCurrentPosition());assertTrue(activity.controller.getShuffleModeEnabled());
                    assertEquals(Player.REPEAT_MODE_ALL,activity.controller.getRepeatMode());
                    activity.controller.setShuffleModeEnabled(false);activity.controller.setRepeatMode(Player.REPEAT_MODE_OFF);
                    activity.togglePlay();
                });
                waitFor(()->{java.util.concurrent.atomic.AtomicBoolean playing=new java.util.concurrent.atomic.AtomicBoolean();main(()->playing.set(activity.controller.isPlaying()&&activity.controller.getCurrentPosition()>=4000));return playing.get();},15);
                main(activity::skipNext);
                waitFor(()->{java.util.concurrent.atomic.AtomicBoolean failed=new java.util.concurrent.atomic.AtomicBoolean();main(()->failed.set(activity.controller.getPlayerError()!=null));return failed.get();},15);
                main(()->{assertEquals(3,activity.controller.getMediaItemCount());activity.skipNext();});
                waitFor(()->{java.util.concurrent.atomic.AtomicBoolean playing=new java.util.concurrent.atomic.AtomicBoolean();main(()->playing.set(activity.controller.isPlaying()&&activity.controller.getCurrentMediaItemIndex()==2));return playing.get();},15);
                main(()->activity.playNow(playable));
                waitFor(()->{final boolean[] ready={false};main(()->ready[0]=activity.controller.getMediaItemCount()==4&&activity.controller.getCurrentMediaItemIndex()==3);return ready[0];},10);
                main(()->{activity.controller.setShuffleModeEnabled(true);activity.addQueue(invalid,true);});
                waitFor(()->{final boolean[] ready={false};main(()->ready[0]=activity.controller.getMediaItemCount()==5);return ready[0];},10);
                main(activity::skipNext);
                waitFor(()->{final boolean[] ready={false};main(()->ready[0]=activity.controller.getCurrentMediaItemIndex()==4);return ready[0];},10);
                main(()->{activity.controller.stop();activity.controller.clearMediaItems();});
            }
        }finally{resolver.delete(uri,null,null);}
    }
    @Test public void layoutAtCurrentSize()throws Exception{
        try(ActivityScenario<MainActivity> scenario=ActivityScenario.launch(MainActivity.class)){
            AtomicReference<MainActivity> ref=new AtomicReference<>();scenario.onActivity(ref::set);MainActivity a=ref.get();waitFor(()->{boolean[] ready={false};main(()->ready[0]=a.controller!=null);return ready[0];},20);
            main(()->{a.controller.stop();a.controller.clearMediaItems();a.results.clear();for(int i=0;i<24;i++)a.results.add(new Track("n61ULEU7CO0","Uzun şarkı adı – telefon ekranında yazı boyutu ve kaydırma kontrolü","Sanatçı",337,""));a.tab=0;a.showPage(true);});
            Thread.sleep(700);main(()->{assertTrue("List height: "+a.list.getHeight(),a.list.getHeight()>=a.dp(40));assertTrue(a.list.canScrollVertically(1));assertTrue(a.list.getWidth()<=a.root.getWidth());for(int i=0;i<a.nav.getChildCount();i++){View v=a.nav.getChildAt(i);assertTrue(v.getHeight()>=a.dp(48));assertTrue(v.getWidth()>=a.dp(48));}});shot("layout");
            main(()->a.addQueue(a.results.get(0)));
            waitFor(()->{final boolean[] ready={false};main(()->ready[0]=a.controller.getMediaItemCount()>0);return ready[0];},10);
            main(a::showPlayer);shot("layout-player");main(()->{assertTrue(a.timeline.getWidth()>0);assertTrue(a.fullPlay.getWidth()>=a.dp(48));a.playerDialog.dismiss();a.controller.clearMediaItems();});
        }
    }
    @Test public void fastDirectSearch()throws Exception{
        MusicApp app=(MusicApp)InstrumentationRegistry.getInstrumentation().getTargetContext().getApplicationContext();long start=System.currentTimeMillis();List<Track> tracks=app.youtube.search("Radiohead No Surprises");assertFalse(tracks.isEmpty());android.util.Log.i("MuzikcalarTest","Direct search elapsed ms="+(System.currentTimeMillis()-start));
    }
}
