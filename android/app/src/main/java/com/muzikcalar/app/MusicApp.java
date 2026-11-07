package com.muzikcalar.app;
import android.app.Application;
import java.io.File;
import java.util.*;
import java.util.concurrent.*;
import okhttp3.Cache;
import okhttp3.OkHttpClient;
public final class MusicApp extends Application {
    public static MusicApp instance;
    public Library library;
    public String libraryError;
    String recoveryStatus="";
    public Youtube youtube;
    CoverLoader covers;
    final ArrayList<Track> recent=new ArrayList<>();
    public final ExecutorService io=Executors.newFixedThreadPool(4);
    public void onCreate(){
        super.onCreate();
        instance = this;
        OkHttpClient http = new OkHttpClient.Builder()
                .connectTimeout(12, TimeUnit.SECONDS).readTimeout(20, TimeUnit.SECONDS)
                .cache(new Cache(new File(getCacheDir(), "http"), 40 * 1024 * 1024)).build();
        youtube = new Youtube(this, http);
        covers = new CoverLoader(http, io);
        try{library=new Library(getFilesDir());}catch(Exception e){libraryError=e.getMessage();}
        try{recent.addAll(Track.list(new org.json.JSONArray(getSharedPreferences("recent",0).getString("tracks","[]"))));}
        catch(org.json.JSONException e){android.util.Log.w("Muzikcalar","Dinleme geçmişi okunamadı",e);}
    }
    void record(Track track){
        if(!recent.isEmpty()&&recent.get(0).id.equals(track.id))return;
        recent.removeIf(t->t.id.equals(track.id));
        recent.add(0,track);
        if(recent.size()>40)recent.remove(recent.size()-1);
        getSharedPreferences("recent",0).edit().putString("tracks",Track.array(recent).toString()).apply();
    }
}
