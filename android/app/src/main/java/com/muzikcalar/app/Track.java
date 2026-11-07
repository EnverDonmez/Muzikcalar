package com.muzikcalar.app;

import android.net.Uri;
import android.os.Bundle;
import androidx.media3.common.MediaItem;
import androidx.media3.common.MediaMetadata;
import org.json.*;
import java.util.*;
import java.util.regex.*;

public final class Track {
    public final String id, title, artist, cover, local;
    public final long seconds;
    public Track(String id, String title, String artist, long seconds, String cover) {
        this(id,title,artist,seconds,cover,"");
    }
    private Track(String id, String title, String artist, long seconds, String cover, String local) {
        if (local.isEmpty() ? !id.matches("[A-Za-z0-9_-]{11}") :
                !local.startsWith("content://") || !id.equals(localId(local)))
            throw new IllegalArgumentException("Geçersiz parça kaydı");
        this.id=id; this.title=title; this.artist=artist; this.seconds=seconds;
        this.local=local;
        this.cover=local.isEmpty()?(cover.isEmpty()?"https://i.ytimg.com/vi/"+id+"/hqdefault.jpg":cover):"";
    }
    public boolean isLocal() { return !local.isEmpty(); }
    public String url() { return isLocal()?local:"https://www.youtube.com/watch?v="+id; }
    private static String localId(String uri) { return "local-"+UUID.nameUUIDFromBytes(uri.getBytes(java.nio.charset.StandardCharsets.UTF_8)); }
    public static Track local(Uri uri,String title) { String value=uri.toString(); return new Track(localId(value),title,"Yerel dosya",0,"",value); }
    public JSONObject json() { try { return new JSONObject().put("id",id).put("title",title).put("channel",artist).put("duration",seconds).put("thumbnail",cover).put("local",local); } catch(JSONException e) { throw new IllegalStateException(e); } }
    public static Track from(JSONObject j) { return new Track(j.optString("id"),j.optString("title","Adsız parça"),j.optString("channel",j.optString("artist",j.optString("uploader","YouTube"))),j.optLong("duration",j.optLong("seconds",0)),j.optString("thumbnail",j.optString("cover","")),j.isNull("local")?"":j.optString("local","")); }
    public MediaItem media() {
        Bundle extras=new Bundle();
        extras.putLong("seconds",seconds);
        if(isLocal())extras.putString("local",local);
        MediaMetadata.Builder metadata=new MediaMetadata.Builder().setTitle(title).setArtist(artist).setExtras(extras);
        if(!cover.isEmpty())metadata.setArtworkUri(Uri.parse(cover));
        return new MediaItem.Builder().setMediaId(id).setUri(isLocal()?local:"muzikcalar://audio/"+id).setMediaMetadata(metadata.build()).build();
    }
    public static Track fromMedia(MediaItem item) {
        MediaMetadata m=item.mediaMetadata;
        Bundle extras=m.extras;
        return new Track(item.mediaId,String.valueOf(m.title),String.valueOf(m.artist),extras==null?0:extras.getLong("seconds"),
                m.artworkUri==null?"":m.artworkUri.toString(),extras==null?"":extras.getString("local",""));
    }
    public static String videoId(String input) {
        Matcher m=Pattern.compile("(?i)(?:^|\\s)(?:https?://)?(?:www\\.|m\\.|music\\.)?(?:youtube\\.com/(?:watch\\?[^\\s]*?v=|shorts/|live/)|youtu\\.be/)([A-Za-z0-9_-]{11})(?:[^A-Za-z0-9_-]|$)").matcher(input);
        return m.find()?m.group(1):null;
    }
    public static String time(long seconds) { if(seconds<0) seconds=0; return seconds>=3600?String.format(Locale.ROOT,"%d:%02d:%02d",seconds/3600,(seconds/60)%60,seconds%60):String.format(Locale.ROOT,"%d:%02d",seconds/60,seconds%60); }
    public static JSONArray array(Collection<Track> tracks) { JSONArray a=new JSONArray(); for(Track t:tracks)a.put(t.json()); return a; }
    public static ArrayList<Track> list(JSONArray a) { ArrayList<Track> out=new ArrayList<>(); if(a!=null) for(int i=0;i<a.length();i++)try{out.add(from(a.getJSONObject(i)));}catch(Exception ignored){} return out; }
}
