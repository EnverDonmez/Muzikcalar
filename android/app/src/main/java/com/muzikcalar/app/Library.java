package com.muzikcalar.app;

import android.util.AtomicFile;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

public final class Library {
    public static final class Playlist {
        public final String id;
        public String name;
        public final ArrayList<Track> tracks=new ArrayList<>();
        Playlist(String id,String name) {this.id=id;this.name=name;}
    }
    public final ArrayList<Track> favorites=new ArrayList<>();
    public final ArrayList<Playlist> playlists=new ArrayList<>();
    private final AtomicFile file;
    private boolean writable=true;
    public Library(File directory) throws IOException {
        file=new AtomicFile(new File(directory,"library.json"));
        if(!file.getBaseFile().exists()&&!new File(directory,"library.json.bak").exists())return;
        try(FileInputStream in=file.openRead()){ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int length;while((length=in.read(buffer))!=-1)bytes.write(buffer,0,length);load(new JSONObject(new String(bytes.toByteArray(),StandardCharsets.UTF_8)));}
        catch(Exception e){writable=false;throw new IOException("Kitaplık okunamadı. Kayıtların korunuyor; boş veri yazılmadı.",e);}
    }
    void load(JSONObject root) throws JSONException {
        if(root.optInt("version")!=1)throw new JSONException("Desteklenmeyen kitaplık");
        favorites.addAll(Track.list(root.optJSONArray("favorites")));
        JSONArray lists=root.optJSONArray("playlists");
        if(lists!=null)for(int i=0;i<lists.length();i++){JSONObject j=lists.getJSONObject(i);Playlist p=new Playlist(j.getString("id"),j.getString("name"));p.tracks.addAll(Track.list(j.optJSONArray("tracks")));playlists.add(p);}
    }
    public void save() throws IOException {
        write(encode(favorites,playlists,false));
    }
    private static JSONObject encode(List<Track> favorites,List<Playlist> playlists,boolean portable) {
        try {
            JSONArray lists=new JSONArray();
            for(Playlist p:playlists)
                lists.put(new JSONObject().put("id",p.id).put("name",p.name).put("tracks",encodeTracks(p.tracks,portable)));
            return new JSONObject().put("version",1).put("favorites",encodeTracks(favorites,portable)).put("playlists",lists);
        } catch(JSONException e) { throw new IllegalStateException(e); }
    }
    private static JSONArray encodeTracks(List<Track> tracks,boolean portable) {
        JSONArray out=new JSONArray();
        for(Track track:tracks)if(!portable||!track.isLocal())out.put(track.json());
        return out;
    }
    public String exportPortable() { return encode(favorites,playlists,true).toString(); }
    private void write(JSONObject data) throws IOException {
        if(!writable)throw new IOException("Kitaplık yazmaya kapalı.");
        FileOutputStream stream=null;
        try { stream=file.startWrite();stream.write(data.toString().getBytes(StandardCharsets.UTF_8));file.finishWrite(stream); }
        catch(Exception e){if(stream!=null)file.failWrite(stream);throw new IOException("Kaydedilemedi; önceki kayıt korunuyor.",e);}
    }
    public void importPortable(String text) throws IOException,JSONException {
        JSONObject data=new JSONObject(text);
        if(data.optInt("version")!=1)throw new JSONException("Desteklenmeyen kitaplık sürümü");
        ArrayList<Track> newFavorites=new ArrayList<>(favorites);
        ArrayList<Playlist> newPlaylists=new ArrayList<>();
        for(Playlist original:playlists){
            Playlist copy=new Playlist(original.id,original.name);
            copy.tracks.addAll(original.tracks);
            newPlaylists.add(copy);
        }
        mergeTracks(data.getJSONArray("favorites"),newFavorites);
        JSONArray source=data.getJSONArray("playlists");
        for(int i=0;i<source.length();i++){
            JSONObject record=source.getJSONObject(i);
            String name=cleanName(record.getString("name"));
            Playlist target=null;
            for(Playlist playlist:newPlaylists)if(nameKey(playlist.name).equals(nameKey(name))){target=playlist;break;}
            if(target==null){target=new Playlist(UUID.randomUUID().toString(),name);newPlaylists.add(target);}
            mergeTracks(record.getJSONArray("tracks"),target.tracks);
        }
        write(encode(newFavorites,newPlaylists,false));
        favorites.clear();favorites.addAll(newFavorites);
        playlists.clear();playlists.addAll(newPlaylists);
    }
    private static void mergeTracks(JSONArray source,List<Track> target) throws JSONException {
        for(int i=0;i<source.length();i++){
            JSONObject record=source.getJSONObject(i);
            if(!record.isNull("local")&&!record.optString("local","").isEmpty())continue;
            Track track=Track.from(record);
            if(!contains(target,track))target.add(track);
        }
    }
    public boolean liked(Track t){return contains(favorites,t);}
    public static boolean contains(List<Track> list,Track t){for(Track a:list)if(a.id.equals(t.id))return true;return false;}
    public static boolean isAt(List<Track> list,int index,Track t){return index>=0&&index<list.size()&&list.get(index).id.equals(t.id);}
    public void toggle(Track t)throws IOException{ArrayList<Track> before=new ArrayList<>(favorites);if(liked(t))favorites.removeIf(a->a.id.equals(t.id));else favorites.add(0,t);try{save();}catch(IOException e){favorites.clear();favorites.addAll(before);throw e;}}
    static String nameKey(String text){return text.toLowerCase(new Locale("tr","TR"));}
    private static String cleanName(String raw){
        String name=raw.trim();
        if(name.isEmpty()||name.length()>80||name.chars().anyMatch(Character::isISOControl))
            throw new IllegalArgumentException("1–80 karakterlik, tek satırlık bir isim yaz.");
        return name;
    }
    private String validName(String raw,String except){String name=cleanName(raw);for(Playlist p:playlists)if(!p.id.equals(except)&&nameKey(p.name).equals(nameKey(name)))throw new IllegalArgumentException("Bu isimde bir playlist zaten var.");return name;}
    public Playlist create(String raw)throws IOException{Playlist p=new Playlist(UUID.randomUUID().toString(),validName(raw,""));playlists.add(p);try{save();}catch(IOException e){playlists.remove(p);throw e;}return p;}
    public void rename(Playlist p,String raw)throws IOException{String before=p.name;p.name=validName(raw,p.id);try{save();}catch(IOException e){p.name=before;throw e;}}
    public void delete(Playlist p)throws IOException{int index=playlists.indexOf(p);if(index<0)return;playlists.remove(index);try{save();}catch(IOException e){playlists.add(index,p);throw e;}}
    public void add(Playlist p,Track t)throws IOException{if(contains(p.tracks,t))return;p.tracks.add(t);try{save();}catch(IOException e){p.tracks.remove(t);throw e;}}
    public void remove(Playlist p,Track t)throws IOException{ArrayList<Track> before=new ArrayList<>(p.tracks);p.tracks.removeIf(a->a.id.equals(t.id));try{save();}catch(IOException e){p.tracks.clear();p.tracks.addAll(before);throw e;}}
    public void move(Playlist playlist,Track track,int direction)throws IOException{
        int index=-1;
        for(int i=0;i<playlist.tracks.size();i++)if(playlist.tracks.get(i).id.equals(track.id)){index=i;break;}
        int to=index+direction;
        if(index<0||to<0||to>=playlist.tracks.size())return;
        Collections.swap(playlist.tracks,index,to);
        try{save();}catch(IOException e){Collections.swap(playlist.tracks,index,to);throw e;}
    }
    public void sort(Playlist playlist,boolean byArtist)throws IOException{
        ArrayList<Track> before=new ArrayList<>(playlist.tracks);
        java.text.Collator comparer=java.text.Collator.getInstance(new Locale("tr","TR"));
        comparer.setStrength(java.text.Collator.SECONDARY);
        playlist.tracks.sort((a,b)->{
            int first=comparer.compare(byArtist?a.artist:a.title,byArtist?b.artist:b.title);
            return first!=0?first:comparer.compare(a.title,b.title);
        });
        try{save();}catch(IOException e){playlist.tracks.clear();playlist.tracks.addAll(before);throw e;}
    }
}
