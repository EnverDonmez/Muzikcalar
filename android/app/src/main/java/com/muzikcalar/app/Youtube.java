package com.muzikcalar.app;

import android.content.Context;
import android.util.LruCache;
import com.yausername.youtubedl_android.*;
import okhttp3.*;
import org.json.*;
import java.io.*;
import java.net.URLEncoder;
import java.util.*;
import java.util.concurrent.TimeUnit;
import java.util.regex.*;

public final class Youtube {
    public static final String AGENT="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private final Context context;
    private final OkHttpClient http;
    private final LruCache<String,SearchEntry> searches=new LruCache<>(24);
    private final LruCache<String,Boolean> verifyOnResolve=new LruCache<>(16);
    private final Map<String,Stream> streams=new HashMap<>();
    private boolean initialized;
    static final class SearchEntry { long time=System.currentTimeMillis();List<Track> tracks;SearchEntry(List<Track> tracks){this.tracks=tracks;} }
    public static final class Stream { public String url; public Map<String,String> headers=new HashMap<>();long time=System.currentTimeMillis(); }
    Youtube(Context context, OkHttpClient http) {
        this.context = context;
        this.http = http;
    }
    synchronized void init()throws Exception{if(!initialized){YoutubeDL.getInstance().init(context);initialized=true;}}
    public String version() throws Exception {
        init();
        YoutubeDLRequest request=new YoutubeDLRequest("");
        request.addOption("--version");
        return YoutubeDL.getInstance().execute(request).getOut().trim();
    }
    public String update(String id)throws Exception{
        init();
        YoutubeDL.getInstance().updateYoutubeDL(context,YoutubeDL.UpdateChannel.STABLE.INSTANCE);
        clearStreams();
        String installed=version();
        verifyNextResolve(id);
        Stream stream=resolve(id);
        Request.Builder probe=new Request.Builder().url(stream.url);
        for(Map.Entry<String,String> header:stream.headers.entrySet())probe.header(header.getKey(),header.getValue());
        probe.header("Range","bytes=0-1023");
        try(Response response=http.newCall(probe.build()).execute()){
            if(!response.isSuccessful()||response.body()==null||response.body().byteStream().read()==-1)
                throw new IOException("Ses akışı doğrulanamadı.");
        }
        return "yt-dlp "+installed+" · Ses akışı doğrulandı.";
    }
    private String get(String url)throws IOException{
        Request request=new Request.Builder().url(url).header("User-Agent",AGENT).header("Accept-Language","tr-TR,tr;q=0.9,en;q=0.8").build();
        try(Response response=http.newBuilder().callTimeout(7,TimeUnit.SECONDS).build().newCall(request).execute()){if(!response.isSuccessful()||response.body()==null)throw new IOException("Bağlantı yanıtı: "+response.code());return response.body().string();}
    }
    public List<Track> search(String raw)throws Exception{
        String query=raw.trim();if(query.isEmpty())return new ArrayList<>();
        SearchEntry cached=searches.get(query);if(cached!=null&&System.currentTimeMillis()-cached.time<600000)return new ArrayList<>(cached.tracks);
        String id=Track.videoId(query);ArrayList<Track> tracks=new ArrayList<>();
        if(id!=null){JSONObject j=new JSONObject(get("https://www.youtube.com/oembed?format=json&url="+URLEncoder.encode("https://www.youtube.com/watch?v="+id,"UTF-8")));tracks.add(new Track(id,j.optString("title"),j.optString("author_name"),0,j.optString("thumbnail_url")));}
        else{
            if(query.matches("(?i)^https?://.*"))throw new IOException("Bir YouTube bağlantısı veya şarkı adı yaz.");
            try{String html=get("https://www.youtube.com/results?search_query="+URLEncoder.encode(query,"UTF-8")+"&sp=EgIQAQ%3D%3D&hl=tr");tracks.addAll(parseSearch(html));}catch(Exception failed){android.util.Log.w("Muzikcalar","Direct search fallback: "+failed.getClass().getSimpleName());}
            if(Thread.currentThread().isInterrupted())throw new InterruptedException();
            if(tracks.isEmpty()){
                init();YoutubeDLRequest request=new YoutubeDLRequest("ytsearch20:"+query);request.addOption("--flat-playlist");request.addOption("--dump-single-json");request.addOption("--socket-timeout","15");request.addOption("--retries","1");
                JSONObject j=new JSONObject(YoutubeDL.getInstance().execute(request,null,null).getOut());JSONArray entries=j.optJSONArray("entries");
                if(entries!=null)for(int i=0;i<entries.length();i++){JSONObject e=entries.optJSONObject(i);if(e!=null&&e.optString("id").matches("[A-Za-z0-9_-]{11}"))tracks.add(Track.from(e));}
            }
        }
        if(Thread.currentThread().isInterrupted())throw new InterruptedException();
        searches.put(query,new SearchEntry(new ArrayList<>(tracks)));return tracks;
    }
    static List<Track> parseSearch(String html)throws JSONException{
        Matcher match=Pattern.compile("(?:var\\s+ytInitialData\\s*=|window\\[\"ytInitialData\"\\]\\s*=|ytInitialData\\s*=)\\s*\\{").matcher(html);if(!match.find())throw new JSONException("Arama verisi bulunamadı");
        int start=match.end()-1;
        int depth=0,end=-1;boolean quoted=false,escaped=false;
        for(int i=start;i<html.length();i++){char c=html.charAt(i);if(quoted){if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='\"')quoted=false;}else if(c=='\"')quoted=true;else if(c=='{')depth++;else if(c=='}'&&--depth==0){end=i+1;break;}}
        if(end<0)throw new JSONException("Arama verisi eksik");LinkedHashMap<String,Track> out=new LinkedHashMap<>();walk(new JSONObject(html.substring(start,end)),out,0);return new ArrayList<>(out.values());
    }
    private static String text(JSONObject j){if(j==null)return "";if(j.has("simpleText"))return j.optString("simpleText");StringBuilder b=new StringBuilder();JSONArray a=j.optJSONArray("runs");if(a!=null)for(int i=0;i<a.length();i++)b.append(a.optJSONObject(i).optString("text"));return b.toString();}
    static long duration(String text){try{long n=0;for(String part:text.split(":"))n=n*60+Long.parseLong(part.replaceAll("[^0-9]",""));return n;}catch(Exception e){return 0;}}
    private static void walk(Object node,LinkedHashMap<String,Track> out,int depth){
        if(depth>60||out.size()>=30)return;
        if(node instanceof JSONObject){JSONObject j=(JSONObject)node;JSONObject v=j.optJSONObject("videoRenderer");if(v!=null){String id=v.optString("videoId");if(id.matches("[A-Za-z0-9_-]{11}")){String title=text(v.optJSONObject("title")),artist=text(v.optJSONObject("ownerText"));if(artist.isEmpty())artist=text(v.optJSONObject("longBylineText"));out.putIfAbsent(id,new Track(id,title,artist,duration(text(v.optJSONObject("lengthText"))),""));}return;}Iterator<String> keys=j.keys();while(keys.hasNext())walk(j.opt(keys.next()),out,depth+1);}
        else if(node instanceof JSONArray){JSONArray a=(JSONArray)node;for(int i=0;i<a.length();i++)walk(a.opt(i),out,depth+1);}
    }
    public Stream resolve(String id)throws IOException{
        synchronized(streams){Stream s=streams.get(id);if(s!=null&&System.currentTimeMillis()-s.time<240000)return s;}
        try{
            init();YoutubeDLRequest request=new YoutubeDLRequest("https://www.youtube.com/watch?v="+id);
            request.addOption("--no-playlist");request.addOption("--skip-download");request.addOption("--dump-single-json");request.addOption("-f","bestaudio/best");request.addOption("--remote-components","ejs:github");request.addOption("--socket-timeout","15");request.addOption("--retries","1");
            if(Boolean.TRUE.equals(verifyOnResolve.get(id)))request.addOption("--check-formats");
            JSONObject j=new JSONObject(YoutubeDL.getInstance().execute(request,"audio-"+id+"-"+UUID.randomUUID(),null).getOut());Stream s=new Stream();s.url=j.getString("url");
            JSONObject headers=j.optJSONObject("http_headers");if(headers!=null){Iterator<String> keys=headers.keys();while(keys.hasNext()){String k=keys.next();s.headers.put(k,headers.getString(k));}}
            if(!s.url.startsWith("https://"))throw new IOException("Güvenli ses akışı bulunamadı.");
            synchronized(streams){streams.put(id,s);}verifyOnResolve.remove(id);return s;
        }catch(Exception e){
            String details=String.valueOf(e.getMessage()).toLowerCase(Locale.ROOT);
            boolean restricted=details.matches("(?s).*(private video|not available|unavailable|age.restricted|sign in).*");
            boolean transientFailure=!restricted&&details.matches("(?s).*(timed out|timeout|connection|network|temporar|http error (403|429|5[0-9][0-9])|signature|nsig|resolve host|failed to resolve|name resolution|getaddrinfo).*");
            throw new SourceFailure(restricted?"YouTube bu parçaya erişim vermiyor.":"Ses bağlantısı alınamadı.",transientFailure,e);
        }
    }
    static final class SourceFailure extends IOException {
        final boolean transientFailure;
        SourceFailure(String message,boolean transientFailure,Throwable cause){super(message,cause);this.transientFailure=transientFailure;}
    }
    public void invalidate(String id){synchronized(streams){streams.remove(id);}}
    public void verifyNextResolve(String id){invalidate(id);verifyOnResolve.put(id,true);}
    public void clearStreams(){synchronized(streams){streams.clear();}verifyOnResolve.evictAll();}
}
