package com.muzikcalar.app;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.net.Uri;
import android.os.*;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.*;
import android.view.inputmethod.*;
import android.widget.*;
import androidx.activity.ComponentActivity;
import androidx.activity.OnBackPressedCallback;
import androidx.core.view.*;
import androidx.media3.common.*;
import androidx.media3.common.util.UnstableApi;
import androidx.media3.session.*;
import androidx.media3.session.MediaController;
import androidx.recyclerview.widget.*;
import com.google.common.util.concurrent.ListenableFuture;
import java.io.IOException;
import java.util.*;
import java.util.concurrent.*;
import okhttp3.Call;

@UnstableApi
public final class MainActivity extends ComponentActivity {
    private static final int LOCAL_AUDIO=100, IMPORT_LIBRARY=101, EXPORT_LIBRARY=102, RECENT=4;
    MusicApp app;
    MediaController controller;
    ListenableFuture<MediaController> controllerFuture;
    LinearLayout root,searchArea,pageHeader,mini,nav;
    AutoCompleteTextView search;
    TextView pageTitle,pageSubtitle,status,miniTitle,miniArtist,empty;
    ImageView miniCover;
    ImageButton miniPlay;
    ProgressBar busy;
    Button pageAction;
    RecyclerView list;
    final Rows adapter=new Rows();
    final ArrayList<Track> results=new ArrayList<>();
    int tab=0,searchVersion;
    Library.Playlist opened;
    Future<?> searchTask;
    String lastQuery="",searchError="",playlistFilter="";
    boolean searching;
    Dialog playerDialog;
    TextView fullTitle,fullArtist,elapsed,total,fullStatus;
    ImageView fullCover;
    ImageButton fullPlay,fullLike,fullRepeat,fullShuffle;
    SeekBar timeline;
    boolean seeking;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final Runnable requestSuggestions=this::fetchSuggestions;
    private Call suggestionCall;
    private int suggestionVersion;
    private boolean searchKeyboardVisible;
    private final Player.Listener listener=new Player.Listener(){public void onEvents(Player player,Player.Events events){
        refreshPlayback();
        if(tab==3&&(events.contains(Player.EVENT_TIMELINE_CHANGED)||events.contains(Player.EVENT_MEDIA_ITEM_TRANSITION))
                ||tab==RECENT&&events.contains(Player.EVENT_IS_PLAYING_CHANGED))showPage(false);
    }};

    public void onCreate(Bundle state){super.onCreate(state);app=(MusicApp)getApplication();Ui.fonts(this);WindowCompat.setDecorFitsSystemWindows(getWindow(),false);getWindow().setStatusBarColor(Ui.BG);getWindow().setNavigationBarColor(Ui.BG);new WindowInsetsControllerCompat(getWindow(),getWindow().getDecorView()).setAppearanceLightStatusBars(false);
        if(state!=null){tab=state.getInt("tab",0);lastQuery=state.getString("query","");playlistFilter=state.getString("playlistFilter","");results.addAll(Track.list(parseArray(state.getString("results","[]"))));String id=state.getString("playlist","");if(app.library!=null)for(Library.Playlist p:app.library.playlists)if(p.id.equals(id))opened=p;}
        build();adaptCompactLayout();search.setText(lastQuery);showPage(false);
        controllerFuture=new MediaController.Builder(this,new SessionToken(this,new ComponentName(this,PlaybackService.class))).buildAsync();
        controllerFuture.addListener(()->{try{controller=controllerFuture.get();controller.addListener(listener);refreshPlayback();if(tab==3)showPage(false);}catch(Exception e){message("Oynatıcı başlatılamadı.");}},androidx.core.content.ContextCompat.getMainExecutor(this));
        getOnBackPressedDispatcher().addCallback(this,new OnBackPressedCallback(true){public void handleOnBackPressed(){if(search.isPopupShowing()){clearSuggestions();}else if(opened!=null){opened=null;showPage(true);}else if(tab!=0){tab=0;showPage(true);}else{setEnabled(false);getOnBackPressedDispatcher().onBackPressed();}}});
        handler.post(ticker);handleShare(getIntent());if(app.libraryError!=null)new AlertDialog.Builder(this).setTitle("Kitaplığın korunuyor").setMessage(app.libraryError).setPositiveButton("Tamam",null).show();
    }
    static org.json.JSONArray parseArray(String raw){try{return new org.json.JSONArray(raw);}catch(Exception e){return new org.json.JSONArray();}}
    @Override protected void onSaveInstanceState(Bundle s){super.onSaveInstanceState(s);s.putInt("tab",tab);s.putString("query",search.getText().toString());s.putString("results",Track.array(results).toString());if(opened!=null){s.putString("playlist",opened.id);s.putString("playlistFilter",playlistFilter);}}
    @Override protected void onNewIntent(Intent i){super.onNewIntent(i);handleShare(i);}
    void handleShare(Intent i){if(Intent.ACTION_SEND.equals(i.getAction())){String text=i.getStringExtra(Intent.EXTRA_TEXT);if(text!=null){tab=0;opened=null;search.setText(text);showPage(true);runSearch();}}}
    int dp(float n){return Ui.dp(this,n);}
    LinearLayout.LayoutParams lp(int w,int h){return new LinearLayout.LayoutParams(w<0?w:dp(w),h<0?h:dp(h));}
    void build(){
        root=Ui.vertical(this);root.setBackgroundColor(Ui.BG);setContentView(root);
        ViewCompat.setOnApplyWindowInsetsListener(root,(v,insets)->{androidx.core.graphics.Insets bars=insets.getInsets(WindowInsetsCompat.Type.systemBars()|WindowInsetsCompat.Type.displayCutout());int keyboard=insets.getInsets(WindowInsetsCompat.Type.ime()).bottom;root.setPadding(bars.left,bars.top,bars.right,Math.max(bars.bottom,keyboard));boolean typing=keyboard>bars.bottom;mini.setVisibility(typing?View.GONE:View.VISIBLE);nav.setVisibility(typing?View.GONE:View.VISIBLE);
            boolean keyboardVisible=insets.isVisible(WindowInsetsCompat.Type.ime());
            if (searchKeyboardVisible && !keyboardVisible) clearSuggestions();
            searchKeyboardVisible=keyboardVisible;
            return insets;
        });
        LinearLayout header=Ui.horizontal(this);header.setPadding(dp(20),dp(12),dp(12),dp(12));ImageView logo=new ImageView(this);logo.setImageResource(R.drawable.brand_logo);logo.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);header.addView(logo,lp(44,44));Ui.space(header,this,12,0);LinearLayout names=Ui.vertical(this);names.addView(Ui.text(this,"Müzikçalar",21,true,Ui.TEXT));TextView tagline=Ui.text(this,"Kendi ritminde.",12,false,Ui.MUTED);tagline.setPadding(0,dp(4),0,0);names.addView(tagline);header.addView(names,new LinearLayout.LayoutParams(0,-2,1));ImageButton settings=Ui.action(this,"settings","Ayarlar ve ses motorunu güncelle",false);settings.setOnClickListener(v->settings());header.addView(settings,lp(48,48));root.addView(header,lp(-1,-2));
        LinearLayout body=Ui.vertical(this);int margin=getResources().getConfiguration().screenWidthDp>=600?40:20;body.setPadding(dp(margin),0,dp(margin),0);root.addView(body,new LinearLayout.LayoutParams(-1,0,1));
        searchArea=Ui.vertical(this);TextView label=Ui.text(this,"MÜZİĞİNİ BUL",11,true,Ui.LIME);label.setPadding(dp(2),dp(8),0,dp(10));searchArea.addView(label);LinearLayout field=Ui.horizontal(this);field.setBackground(Ui.shape(this,Ui.SURFACE,18));field.setPadding(dp(16),dp(2),dp(4),dp(2));
        search=new AutoCompleteTextView(this) {
            // The suggestion service filters each query before the adapter is populated.
            @Override protected void performFiltering(CharSequence text, int keyCode) { }
        };
        search.setTextColor(Ui.TEXT);
        search.setHintTextColor(Ui.MUTED);
        search.setHint("Şarkı, sanatçı veya YouTube linki");
        search.setTextSize(14);
        search.setTypeface(Ui.regular);
        search.setSingleLine(true);
        search.setBackgroundColor(Color.TRANSPARENT);
        search.setImeOptions(EditorInfo.IME_ACTION_SEARCH|EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        search.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
        search.setContentDescription("Şarkı, sanatçı veya YouTube bağlantısı ara");
        search.setMinimumHeight(dp(52));
        field.addView(search,new LinearLayout.LayoutParams(0,-2,1));
        ImageButton find=Ui.action(this,"search","Ara",true);
        find.setOnClickListener(v->runSearch());
        field.addView(find,lp(48,48));
        search.setOnEditorActionListener((v,action,event)->{
            if (action!=EditorInfo.IME_ACTION_SEARCH) return false;
            runSearch();
            return true;
        });
        searchArea.addView(field,lp(-1,-2));
        body.addView(searchArea);
        buildSearchSuggestions(field);
        pageHeader=Ui.horizontal(this);pageHeader.setPadding(0,dp(24),0,dp(12));LinearLayout titles=Ui.vertical(this);pageTitle=Ui.text(this,"Bugün ne dinleyelim?",22,true,Ui.TEXT);pageTitle.setMaxLines(2);titles.addView(pageTitle);pageSubtitle=Ui.text(this,"Sevdiğin müziğe bir arama uzaktasın.",12,false,Ui.MUTED);pageSubtitle.setPadding(0,dp(8),0,0);pageSubtitle.setMaxLines(2);titles.addView(pageSubtitle);pageHeader.addView(titles,new LinearLayout.LayoutParams(0,-2,1));pageAction=Ui.button(this,"Yeni",true);pageAction.setOnClickListener(v->{if(tab==2&&opened==null)namePlaylist(null,null);else playVisible();});pageHeader.addView(pageAction,lp(-2,-2));body.addView(pageHeader);
        busy=new ProgressBar(this,null,android.R.attr.progressBarStyleHorizontal);busy.setIndeterminate(true);busy.setIndeterminateTintList(ColorStateList.valueOf(Ui.LIME));body.addView(busy,lp(-1,3));
        status=Ui.text(this,"",12,false,Ui.MUTED);status.setPadding(0,dp(8),0,dp(8));body.addView(status,lp(-1,-2));
        FrameLayout frame=new FrameLayout(this);body.addView(frame,new LinearLayout.LayoutParams(-1,0,1));list=new RecyclerView(this);list.setLayoutManager(new LinearLayoutManager(this));list.setAdapter(adapter);list.setItemAnimator(null);list.setClipToPadding(false);list.setPadding(0,0,0,dp(12));frame.addView(list,new FrameLayout.LayoutParams(-1,-1));empty=Ui.text(this,"",15,false,Ui.MUTED);empty.setGravity(Gravity.CENTER);empty.setPadding(dp(20),dp(16),dp(20),dp(16));empty.setLineSpacing(dp(6),1);frame.addView(empty,new FrameLayout.LayoutParams(-1,-1));
        buildMini();buildNav();
    }
    private void buildSearchSuggestions(View field) {
        field.setId(View.generateViewId());
        search.setDropDownAnchor(field.getId());
        search.setDropDownVerticalOffset(dp(4));
        search.setDropDownBackgroundDrawable(Ui.shape(this,Ui.RAISED,14));
        search.setOnItemClickListener((parent,row,position,id)->runSearch());
        search.setOnFocusChangeListener((view,focused)->{if (!focused) clearSuggestions();});
        search.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence text,int start,int count,int after) { }
            @Override public void onTextChanged(CharSequence text,int start,int before,int count) { }
            @Override public void afterTextChanged(Editable text) {
                if (search.isPerformingCompletion()) return;
                clearSuggestions();
                if (tab==0 && search.hasFocus() && Youtube.canSuggest(text.toString().trim())) {
                    handler.postDelayed(requestSuggestions,250);
                }
            }
        });
    }

    private void fetchSuggestions() {
        if (tab!=0 || !search.hasFocus() || !hasWindowFocus()) return;
        String query=search.getText().toString().trim();
        int version=suggestionVersion;
        suggestionCall=app.youtube.suggest(query,found->handler.post(()->{
            if (version!=suggestionVersion || tab!=0 || !search.hasFocus() || !hasWindowFocus()) return;
            suggestionCall=null;
            if (found.isEmpty()) return;
            search.setAdapter(new ArrayAdapter<String>(this,android.R.layout.simple_dropdown_item_1line,found) {
                @Override public View getView(int position, View recycled, ViewGroup parent) {
                    TextView row=(TextView)super.getView(position,recycled,parent);
                    row.setTextColor(Ui.TEXT);
                    row.setTypeface(Ui.regular);
                    row.setTextSize(14);
                    row.setMinHeight(dp(48));
                    row.setPadding(dp(16),dp(4),dp(16),dp(4));
                    row.setEllipsize(TextUtils.TruncateAt.END);
                    return row;
                }
            });
            search.showDropDown();
        }));
    }

    private void clearSuggestions() {
        suggestionVersion++;
        handler.removeCallbacks(requestSuggestions);
        if (suggestionCall!=null) {
            suggestionCall.cancel();
            suggestionCall=null;
        }
        if (search.getAdapter()!=null) {
            search.dismissDropDown();
            search.setAdapter(null);
        }
    }

    @Override public void onWindowFocusChanged(boolean focused) {
        super.onWindowFocusChanged(focused);
        if (!focused && search!=null) clearSuggestions();
    }
    void buildMini(){
        mini=Ui.horizontal(this);mini.setPadding(dp(12),dp(8),dp(6),dp(8));mini.setBackground(Ui.ripple(this,Ui.SURFACE,18));LinearLayout.LayoutParams box=lp(-1,-2);box.setMargins(dp(12),dp(6),dp(12),dp(6));root.addView(mini,box);miniCover=new ImageView(this);miniCover.setScaleType(ImageView.ScaleType.CENTER_CROP);miniCover.setImageDrawable(Ui.icon(this,"music",Ui.LIME));mini.addView(miniCover,lp(52,48));Ui.space(mini,this,12,0);LinearLayout texts=Ui.vertical(this);miniTitle=Ui.text(this,"Dinlemeye hazırsın",13,true,Ui.TEXT);miniTitle.setSingleLine(true);miniTitle.setEllipsize(TextUtils.TruncateAt.END);miniArtist=Ui.text(this,"Bir parça seç, ritmini bul.",11,false,Ui.MUTED);miniArtist.setSingleLine(true);miniArtist.setEllipsize(TextUtils.TruncateAt.END);miniArtist.setPadding(0,dp(5),0,0);texts.addView(miniTitle);texts.addView(miniArtist);mini.addView(texts,new LinearLayout.LayoutParams(0,-2,1));miniPlay=Ui.action(this,"play","Çal veya duraklat",true);miniPlay.setOnClickListener(v->togglePlay());mini.addView(miniPlay,lp(48,48));mini.setOnClickListener(v->showPlayer());
    }
    void buildNav(){
        nav=Ui.horizontal(this);nav.setPadding(dp(8),dp(4),dp(8),dp(6));String[] labels={"Keşfet","Beğeniler","Playlistler","Sıra","Geçmiş"};for(int i=0;i<labels.length;i++){final int target=i;LinearLayout item=Ui.vertical(this);item.setGravity(Gravity.CENTER);item.setPadding(dp(2),dp(8),dp(2),dp(6));item.setBackground(Ui.ripple(this,Color.TRANSPARENT,14));item.setContentDescription(i==RECENT?"Son dinlenenler":labels[i]);item.setFocusable(true);item.setClickable(true);ImageView image=new ImageView(this);image.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);item.addView(image,lp(22,22));TextView label=Ui.text(this,labels[i],10,true,Ui.MUTED);label.setPadding(0,dp(6),0,0);label.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);item.addView(label);item.setOnClickListener(v->{tab=target;opened=null;playlistFilter="";hideKeyboard();showPage(true);});nav.addView(item,new LinearLayout.LayoutParams(0,-2,1));}root.addView(nav,lp(-1,-2));
    }
    void adaptCompactLayout(){
        boolean shortScreen=getResources().getConfiguration().screenHeightDp<500;
        boolean largeText=getResources().getConfiguration().fontScale>=1.4f;
        if(shortScreen){
            LinearLayout header=(LinearLayout)root.getChildAt(0);View settings=header.getChildAt(3);header.removeView(settings);mini.addView(settings,lp(48,48));header.setVisibility(View.GONE);
            searchArea.getChildAt(0).setVisibility(View.GONE);pageHeader.setPadding(0,dp(8),0,dp(8));pageSubtitle.setVisibility(View.GONE);pageTitle.setTextSize(18);pageTitle.setSingleLine(true);pageTitle.setEllipsize(TextUtils.TruncateAt.END);
        }
        if(shortScreen||largeText)for(int i=0;i<nav.getChildCount();i++){LinearLayout item=(LinearLayout)nav.getChildAt(i);item.getChildAt(1).setVisibility(View.GONE);item.setPadding(0,dp(8),0,dp(8));item.setMinimumHeight(dp(48));item.setTooltipText(item.getContentDescription());}
    }
    void showPage(boolean top){
        if (tab!=0) clearSuggestions();
        boolean explore=tab==0;searchArea.setVisibility(explore?View.VISIBLE:View.GONE);pageAction.setVisibility(View.GONE);busy.setVisibility(explore&&searching?View.VISIBLE:View.GONE);status.setText(explore?searchError:"");status.setVisibility(explore&&!searchError.isEmpty()?View.VISIBLE:View.GONE);
        String heading,description,blank;
        if(tab==0){heading=results.isEmpty()?"Bugün ne dinleyelim?":"Arama sonuçları";description=searching?"Müziğin aranıyor…":results.isEmpty()?"Sevdiğin müziğe bir arama uzaktasın.":results.size()+" parça · "+lastQuery;blank=searching?"Kapaklar ve parçalar hazırlanıyor…":"Bir şarkı, bir sanatçı,\nyepyeni bir ruh hali.\n\nÜstten arayarak başla.";}
        else if(tab==1){heading="Beğendiklerin";description="Dönüp dönüp dinlediklerin.";blank="Bir parçanın kalbine dokun.\nSevdiklerin burada biriksin.";}
        else if(tab==2&&opened==null){heading="Playlistlerin";description="Her ruh haline bir liste.";blank="İlk playlist’ini oluştur.\nKendi müzik dünyanı kur.";pageAction.setVisibility(View.VISIBLE);pageAction.setText("+ Yeni");}
        else if(tab==2){heading=opened.name;description=playlistFilter.isEmpty()?opened.tracks.size()+" parça · Liste seçenekleri için başlığa dokun":"Playlist filtresi · "+playlistFilter;blank=playlistFilter.isEmpty()?"Bu liste henüz sessiz.\nBir şarkının menüsünden buraya ekle.":"Bu filtreyle eşleşen parça yok.";pageAction.setVisibility(visibleTracks().isEmpty()?View.GONE:View.VISIBLE);pageAction.setText("Listeyi çal");}
        else if(tab==RECENT){heading="Son dinlenenler";description="Yakın zamanda çaldığın parçalar.";blank="Bir parça çaldığında burada görünür.";}
        else{heading="Çalma sıran";description="Şimdi çalan ve sıradaki parçalar.";blank="Çalma sıran boş.\nŞarkı menüsünden sıraya ekle.";}
        if(tab==1&&!visibleTracks().isEmpty()){pageAction.setVisibility(View.VISIBLE);pageAction.setText("Çal");}
        pageTitle.setText(heading);pageSubtitle.setText(description);pageHeader.setOnClickListener(tab==2&&opened!=null?v->playlistMenu(opened):null);pageHeader.setClickable(tab==2&&opened!=null);pageHeader.setContentDescription(tab==2&&opened!=null?opened.name+", playlist seçenekleri":null);
        adapter.refresh();empty.setText(blank);boolean isEmpty=tab==2&&opened==null?(app.library==null||app.library.playlists.isEmpty()):visibleTracks().isEmpty();empty.setVisibility(isEmpty?View.VISIBLE:View.GONE);list.setVisibility(isEmpty?View.GONE:View.VISIBLE);if(top)list.scrollToPosition(0);
        for(int i=0;i<nav.getChildCount();i++){LinearLayout item=(LinearLayout)nav.getChildAt(i);int color=i==tab?Ui.LIME:Ui.MUTED;((ImageView)item.getChildAt(0)).setImageDrawable(Ui.icon(this,new String[]{"search","heart","library","queue","history"}[i],color));((TextView)item.getChildAt(1)).setTextColor(color);item.setSelected(i==tab);}
    }
    List<Track> visibleTracks(){
        if(tab==0)return results;
        if(tab==RECENT)return app.recent;
        if(tab==1)return app.library==null?Collections.emptyList():app.library.favorites;
        if(tab==2){
            if(opened==null)return Collections.emptyList();
            if(playlistFilter.isEmpty())return opened.tracks;
            ArrayList<Track> filtered=new ArrayList<>();
            String query=Library.nameKey(playlistFilter);
            for(Track track:opened.tracks)
                if(Library.nameKey(track.title+" "+track.artist).contains(query))filtered.add(track);
            return filtered;
        }
        ArrayList<Track> tracks=new ArrayList<>();
        if(controller!=null)for(int i=0;i<controller.getMediaItemCount();i++)tracks.add(fromMedia(controller.getMediaItemAt(i)));
        return tracks;
    }
    static Track fromMedia(MediaItem m){return Track.fromMedia(m);}
    void hideKeyboard(){clearSuggestions();((InputMethodManager)getSystemService(INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(search.getWindowToken(),0);search.clearFocus();}
    void runSearch(){String query=search.getText().toString().trim();if(query.isEmpty()){search.requestFocus();return;}hideKeyboard();lastQuery=query;searchError="";searching=true;tab=0;opened=null;int version=++searchVersion;if(searchTask!=null)searchTask.cancel(true);showPage(false);searchTask=app.io.submit(()->{try{List<Track> found=app.youtube.search(query);runOnUiThread(()->{if(isDestroyed()||version!=searchVersion)return;searching=false;results.clear();results.addAll(found);if(found.isEmpty())searchError="Sonuç bulunamadı. Başka bir arama dene.";showPage(true);});}catch(Exception e){runOnUiThread(()->{if(isDestroyed()||version!=searchVersion)return;searching=false;searchError="Arama tamamlanamadı. Bağlantını kontrol edip tekrar dene.";showPage(false);});}});}
    void requestNotification(){
        SharedPreferences prefs=getSharedPreferences("ui",0);
        if(Build.VERSION.SDK_INT>=33&&!prefs.getBoolean("notificationAsked",false)
                &&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=android.content.pm.PackageManager.PERMISSION_GRANTED){
            prefs.edit().putBoolean("notificationAsked",true).apply();
            requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},44);
        }
    }
    void playTracks(List<Track> tracks,int index){if(controller==null){message("Oynatıcı hazırlanıyor…");return;}if(index<0||index>=tracks.size())return;requestNotification();ArrayList<MediaItem> items=new ArrayList<>();for(Track t:new ArrayList<>(tracks))items.add(t.media());controller.setMediaItems(items,index,0);controller.prepare();controller.play();refreshPlayback();}
    void playVisible(){playTracks(visibleTracks(),0);}
    void togglePlay(){
        if(controller==null||controller.getMediaItemCount()==0){message("Önce bir parça seç.");return;}
        if(controller.getPlayerError()!=null){
            if(!app.recoveryStatus.isEmpty()){controller.pause();app.recoveryStatus="";return;}
            app.youtube.invalidate(controller.getCurrentMediaItem().mediaId);
            controller.prepare();controller.play();
        }else if(controller.getPlayWhenReady())controller.pause();
        else{if(controller.getPlaybackState()==Player.STATE_IDLE||controller.getPlaybackState()==Player.STATE_ENDED)controller.prepare();controller.play();}
    }
    void playNow(Track track){
        queueTrack(track,PlaybackService.PLAY_NOW);
    }
    private void playQueueItem(int index){
        if(controller==null||index<0||index>=controller.getMediaItemCount())return;
        requestNotification();
        controller.seekToDefaultPosition(index);
        if(controller.getPlaybackState()==Player.STATE_IDLE||controller.getPlayerError()!=null)controller.prepare();
        controller.play();
    }
    void skipNext(){
        queueTrack(null,PlaybackService.SKIP_NEXT);
    }
    void addQueue(Track t){addQueue(t,false);}
    void addQueue(Track t,boolean next){
        queueTrack(t,next?PlaybackService.PLAY_NEXT:PlaybackService.APPEND);
    }
    private void queueTrack(Track track,int mode){
        if(controller==null){message("Oynatıcı hazırlanıyor…");return;}
        if(mode==PlaybackService.PLAY_NOW)requestNotification();
        Bundle args=new Bundle();
        args.putInt("mode",mode);
        if(track!=null)args.putString("track",track.json().toString());
        ListenableFuture<SessionResult> result=controller.sendCustomCommand(PlaybackService.QUEUE_TRACK,args);
        result.addListener(()->{
            try{
                if(result.get().resultCode!=SessionResult.RESULT_SUCCESS){
                    message(mode==PlaybackService.SKIP_NEXT?"Sırada başka parça yok.":"Çalma sırası düzenlenemedi.");return;
                }
                if(mode==PlaybackService.PLAY_NEXT||mode==PlaybackService.APPEND)
                    message(mode==PlaybackService.PLAY_NEXT?"Sırada sonraki.":"Sıranın sonuna eklendi.");
            }catch(Exception e){message("Çalma sırası düzenlenemedi.");}
        },androidx.core.content.ContextCompat.getMainExecutor(this));
    }
    void like(Track t){if(app.library==null){message("Kitaplık okunamadığı için değişiklik kaydedilmiyor.");return;}try{app.library.toggle(t);if(tab==1)showPage(false);else adapter.notifyDataSetChanged();refreshPlayback();}catch(Exception e){message(e.getMessage());}}
    void trackMenu(Track t,int index){
        LinkedHashMap<String,Runnable> actions=new LinkedHashMap<>();
        final boolean queuePage=tab==3;
        final Library.Playlist playlist=tab==2?opened:null;
        MediaItem entry=queuePage&&controller!=null&&index<controller.getMediaItemCount()?controller.getMediaItemAt(index):null;
        actions.put("Şimdi çal",()->{if(queuePage)editQueueItem(entry,index,0,false);else playNow(t);});
        actions.put("Sırada sonraki çal",()->addQueue(t,true));
        actions.put("Sıranın sonuna ekle",()->addQueue(t));
        if(!queuePage&&tab!=RECENT){
            List<Track> collection=new ArrayList<>(visibleTracks());
            // The full player opens this menu with a queue index, which does not address the visible list.
            if(Library.isAt(collection,index,t))
                actions.put("Bu parçadan başlayarak listeyi çal",()->playTracks(collection,index));
        }
        actions.put(app.library!=null&&app.library.liked(t)?"Beğenilerden çıkar":"Beğen",()->like(t));
        actions.put("Playlist’e ekle",()->addToPlaylist(t));
        actions.put("Parça bilgisi",()->new AlertDialog.Builder(this).setTitle(t.title)
                .setMessage(t.isLocal()?t.artist:t.artist+"\n\n"+t.url()).setPositiveButton("Tamam",null).show());
        if(playlist!=null){
            actions.put("Bu playlist’ten çıkar",()->editPlaylist(()->app.library.remove(playlist,t)));
            int original=playlist.tracks.indexOf(t);
            if(original>0)actions.put("Playlist’te yukarı taşı",()->editPlaylist(()->app.library.move(playlist,t,-1)));
            if(original>=0&&original+1<playlist.tracks.size())actions.put("Playlist’te aşağı taşı",()->editPlaylist(()->app.library.move(playlist,t,1)));
        }
        if(queuePage&&entry!=null){
            actions.put("Sıradan çıkar",()->editQueueItem(entry,index,0,true));
            if(index>0)actions.put("Sırada yukarı taşı",()->editQueueItem(entry,index,-1,false));
            if(index+1<controller.getMediaItemCount())actions.put("Sırada aşağı taşı",()->editQueueItem(entry,index,1,false));
        }
        showActions(t.title,actions);
    }
    private void editQueueItem(MediaItem entry,int index,int direction,boolean remove){
        if(entry==null||controller==null||index>=controller.getMediaItemCount()||!entry.equals(controller.getMediaItemAt(index))){
            message("Sıra değişti. Parçayı yeniden seç.");return;
        }
        if(remove)controller.removeMediaItem(index);
        else if(direction==0)playQueueItem(index);
        else if(index+direction>=0&&index+direction<controller.getMediaItemCount())controller.moveMediaItem(index,index+direction);
    }
    private interface LibraryEdit{void run()throws IOException;}
    private void editPlaylist(LibraryEdit edit){try{edit.run();showPage(false);}catch(Exception e){message(e.getMessage());}}
    private void showActions(String title,LinkedHashMap<String,Runnable> actions){
        String[] labels=actions.keySet().toArray(new String[0]);
        new AlertDialog.Builder(this).setTitle(title).setItems(labels,(d,i)->actions.get(labels[i]).run()).show();
    }
    void addToPlaylist(Track t){if(app.library==null){message("Kitaplık kullanılamıyor.");return;}ArrayList<String> options=new ArrayList<>();options.add("+ Yeni playlist oluştur");for(Library.Playlist p:app.library.playlists)options.add(p.name);new AlertDialog.Builder(this).setTitle("Playlist’e ekle").setItems(options.toArray(new String[0]),(d,i)->{if(i==0)namePlaylist(null,t);else try{Library.Playlist p=app.library.playlists.get(i-1);app.library.add(p,t);message(p.name+" listesine eklendi.");if(tab==2)showPage(false);}catch(Exception e){message(e.getMessage());}}).show();}
    void namePlaylist(Library.Playlist p,Track add){if(app.library==null){message("Kitaplık kullanılamıyor.");return;}EditText input=new EditText(this);input.setTextColor(Ui.TEXT);input.setTextSize(16);input.setTypeface(Ui.regular);input.setSingleLine(true);input.setHint("Playlist adı");input.setContentDescription("Playlist adı");input.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(80)});if(p!=null){input.setText(p.name);input.selectAll();}LinearLayout container=Ui.vertical(this);container.setPadding(dp(24),dp(8),dp(24),0);container.addView(input,lp(-1,56));AlertDialog dialog=new AlertDialog.Builder(this).setTitle(p==null?"Yeni playlist":"Playlist adını değiştir").setView(container).setPositiveButton("Kaydet",null).setNegativeButton("Vazgeç",null).create();dialog.setOnShowListener(d->{dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v->{try{if(p==null){Library.Playlist created=app.library.create(input.getText().toString());if(add!=null)app.library.add(created,add);}else app.library.rename(p,input.getText().toString());dialog.dismiss();showPage(false);message("Kaydedildi.");}catch(Exception e){input.setError(e.getMessage());}});input.requestFocus();dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_VISIBLE);});dialog.show();}
    void playlistMenu(Library.Playlist playlist){
        LinkedHashMap<String,Runnable> actions=new LinkedHashMap<>();
        actions.put("Listeyi çal",()->playTracks(opened==playlist?visibleTracks():playlist.tracks,0));
        actions.put("Playlist içinde ara…",()->filterPlaylist(playlist));
        if(opened==playlist&&!playlistFilter.isEmpty())actions.put("Filtreyi temizle",()->{playlistFilter="";showPage(true);});
        actions.put("Parça adına göre sırala",()->editPlaylist(()->app.library.sort(playlist,false)));
        actions.put("Sanatçıya göre sırala",()->editPlaylist(()->app.library.sort(playlist,true)));
        actions.put("Adını değiştir",()->namePlaylist(playlist,null));
        actions.put("Playlist’i sil",()->new AlertDialog.Builder(this).setTitle("Playlist silinsin mi?")
                .setMessage(playlist.name+" listesini silmek istiyor musun?").setNegativeButton("Vazgeç",null)
                .setPositiveButton("Sil",(d,i)->editPlaylist(()->{app.library.delete(playlist);if(opened==playlist)opened=null;})).show());
        showActions(playlist.name,actions);
    }
    private void filterPlaylist(Library.Playlist playlist){
        EditText input=new EditText(this);
        input.setSingleLine(true);input.setHint("Parça veya sanatçı");input.setContentDescription("Playlist arama metni");
        input.setText(opened==playlist?playlistFilter:"");
        LinearLayout container=Ui.vertical(this);
        container.setPadding(dp(24),dp(8),dp(24),0);container.addView(input,lp(-1,56));
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Playlist içinde ara").setView(container)
                .setNegativeButton("Vazgeç",null).setPositiveButton("Ara",(d,i)->{opened=playlist;playlistFilter=input.getText().toString().trim();showPage(true);}).create();
        input.requestFocus();dialog.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_VISIBLE);dialog.show();
    }
    void settings(){
        LinkedHashMap<String,Runnable> actions=new LinkedHashMap<>();
        actions.put("Yerel müzik aç…",()->pickDocument(LOCAL_AUDIO));
        if(app.library!=null){
            actions.put("Kitaplığı dışa aktar…",()->pickDocument(EXPORT_LIBRARY));
            actions.put("Kitaplığı içe aktar…",()->pickDocument(IMPORT_LIBRARY));
        }
        actions.put("Ses motorunu güncelle",this::updateEngine);
        actions.put("Önbellekteki ses bağlantılarını yenile",()->{app.youtube.clearStreams();message("Ses bağlantıları yenilendi.");});
        actions.put("Hakkında ve bileşenler",this::showAbout);
        showActions("Müzikçalar",actions);
    }
    private boolean updatingEngine;
    private void updateEngine(){
        if(updatingEngine){message("Ses motoru zaten güncelleniyor.");return;}
        updatingEngine=true;message("Ses motoru güncelleniyor ve doğrulanıyor…");
        MediaItem current=controller==null?null:controller.getCurrentMediaItem();
        String probe=current!=null&&!fromMedia(current).isLocal()?current.mediaId:"n61ULEU7CO0";
        app.io.execute(()->{
            String outcome;
            try{outcome=app.youtube.update(probe);}
            catch(Exception e){outcome="Güncelleme veya ses akışı doğrulanamadı. Bağlantını kontrol et.";android.util.Log.w("Muzikcalar","Motor doğrulaması başarısız",e);}
            String result=outcome;
            runOnUiThread(()->{updatingEngine=false;if(!isDestroyed())message(result);});
        });
    }
    private void showAbout(){
        String info="Kişisel YouTube ve yerel ses oynatıcısı. Beğeniler ve playlistler JSON dosyasıyla taşınabilir.\n\n"
                +"yt-dlp / youtubedl-android (GPLv3)\nAndroidX Media3 (Apache 2.0)\nOkHttp (Apache 2.0)\nMontserrat (SIL OFL)";
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Müzikçalar").setMessage(info+"\n\nSes motoru sürümü okunuyor…")
                .setPositiveButton("Tamam",null).show();
        app.io.execute(()->{
            String version;
            try{version="yt-dlp "+app.youtube.version();}
            catch(Exception e){version="Ses motoru sürümü okunamadı.";}
            String installed=version;
            runOnUiThread(()->{if(dialog.isShowing()&&!isDestroyed())dialog.setMessage(info+"\n\n"+installed);});
        });
    }
    private void pickDocument(int operation){
        Intent picker=new Intent(operation==EXPORT_LIBRARY?Intent.ACTION_CREATE_DOCUMENT:Intent.ACTION_OPEN_DOCUMENT);
        picker.addCategory(Intent.CATEGORY_OPENABLE);
        picker.setType(operation==LOCAL_AUDIO?"audio/*":operation==IMPORT_LIBRARY?"*/*":"application/json");
        if(operation==LOCAL_AUDIO)picker.putExtra(Intent.EXTRA_ALLOW_MULTIPLE,true);
        if(operation==EXPORT_LIBRARY)picker.putExtra(Intent.EXTRA_TITLE,"muzikcalar-kitaplik.json");
        try{startActivityForResult(picker,operation);}
        catch(ActivityNotFoundException e){message("Sistem dosya seçicisi açılamadı.");}
    }
    @Override protected void onActivityResult(int request,int result,Intent data){
        super.onActivityResult(request,result,data);
        if(result!=RESULT_OK||data==null)return;
        Uri uri=data.getData();
        if(request==LOCAL_AUDIO){openLocalMusic(data);return;}
        if((request!=IMPORT_LIBRARY&&request!=EXPORT_LIBRARY)||uri==null||app.library==null)return;
        String snapshot=request==EXPORT_LIBRARY?app.library.exportPortable():null;
        app.io.execute(()->{
            try{
                if(request==EXPORT_LIBRARY){
                    MusicDocuments.writeLibrary(getContentResolver(),uri,snapshot);
                    runOnUiThread(()->message("Kitaplık aktarıldı. Cihazdaki ses dosyaları aktarım dosyasına eklenmedi."));
                }else{
                    String imported=MusicDocuments.readLibrary(getContentResolver(),uri);
                    runOnUiThread(()->{
                        if(isDestroyed())return;
                        try{
                            String openedId=opened==null?null:opened.id;
                            app.library.importPortable(imported);
                            if(openedId!=null)for(Library.Playlist p:app.library.playlists)if(p.id.equals(openedId)){opened=p;break;}
                            showPage(false);message("Kitaplık mevcut kayıtlarla birleştirildi.");
                        }catch(Exception e){message("Geçerli bir Müzikçalar kitaplığı seç.");}
                    });
                }
            }catch(Exception e){runOnUiThread(()->message("Kitaplık dosyasına erişilemedi."));}
        });
    }
    private void openLocalMusic(Intent data){
        app.io.execute(()->{
            List<Track> tracks=MusicDocuments.readAudioSelection(getContentResolver(),data);
            runOnUiThread(()->{
                if(isDestroyed())return;
                if(tracks.isEmpty()){message("Ses dosyasına erişilemedi.");return;}
                if(controller==null){message("Oynatıcı hazırlanıyor…");return;}
                playNow(tracks.get(0));
                if(tracks.size()>1){
                    ArrayList<MediaItem> rest=new ArrayList<>();
                    for(Track track:tracks.subList(1,tracks.size()))rest.add(track.media());
                    controller.addMediaItems(rest);message(rest.size()+" parça sıranın sonuna eklendi.");
                }
            });
        });
    }
    void message(String text){if(isDestroyed())return;Toast.makeText(this,text==null?"İşlem tamamlanamadı.":text,Toast.LENGTH_LONG).show();}
    private final Runnable ticker=new Runnable(){public void run(){if(!isDestroyed()){refreshPlayback();handler.postDelayed(this,500);}}};
    void refreshPlayback(){
        if(controller==null)return;
        MediaItem item=controller.getCurrentMediaItem();
        boolean error=controller.getPlayerError()!=null;
        boolean buffering=controller.getPlaybackState()==Player.STATE_BUFFERING;
        boolean recovering=error&&!app.recoveryStatus.isEmpty();
        boolean playing=controller.getPlayWhenReady()&&controller.getPlaybackState()!=Player.STATE_ENDED&&(!error||recovering);
        String control=playing?(recovering?"Yeniden denemeyi durdur":"Duraklat"):error?"Tekrar dene":"Çal / devam et";
        String state=recovering?app.recoveryStatus:error?"Ses açılamadı · Tekrar dene veya sonraki parçaya geç":
                buffering?"Ses hazırlanıyor…":controller.isPlaying()?"Şimdi çalıyor":playing?"Hazırlanıyor…":"Duraklatıldı";
        miniPlay.setImageDrawable(Ui.icon(this,playing?"pause":"play",Ui.INK));
        miniPlay.setContentDescription(control);miniPlay.setEnabled(item!=null);miniPlay.setAlpha(item==null?.45f:1);
        if(item!=null){Track t=fromMedia(item);miniTitle.setText(t.title);miniArtist.setText(controller.getPlaybackState()==Player.STATE_BUFFERING||controller.getPlayerError()!=null?state:t.artist);if(!t.cover.equals(miniCover.getTag()))app.covers.load(t.cover,miniCover);if(playerDialog!=null&&playerDialog.isShowing()){fullTitle.setText(t.title);fullArtist.setText(t.artist);fullStatus.setText(state);if(!t.cover.equals(fullCover.getTag()))app.covers.load(t.cover,fullCover);fullPlay.setImageDrawable(Ui.icon(this,playing?"pause":"play",Ui.INK));fullPlay.setContentDescription(control);boolean liked=app.library!=null&&app.library.liked(t);fullLike.setImageDrawable(Ui.icon(this,"heart",liked?Ui.LIME:Ui.TEXT));fullLike.setSelected(liked);fullLike.setContentDescription(liked?"Beğenilerden çıkar":"Beğen");fullRepeat.setImageDrawable(Ui.icon(this,"repeat",controller.getRepeatMode()==Player.REPEAT_MODE_OFF?Ui.MUTED:Ui.LIME));fullRepeat.setContentDescription("Tekrar: "+(controller.getRepeatMode()==Player.REPEAT_MODE_ONE?"aynı parça":controller.getRepeatMode()==Player.REPEAT_MODE_ALL?"tüm sıra":"kapalı"));fullShuffle.setImageDrawable(Ui.icon(this,"shuffle",controller.getShuffleModeEnabled()?Ui.LIME:Ui.MUTED));fullShuffle.setContentDescription("Karışık çalma: "+(controller.getShuffleModeEnabled()?"açık":"kapalı"));long duration=controller.getDuration();if(duration==C.TIME_UNSET||duration<0)duration=t.seconds*1000;timeline.setMax((int)Math.min(Integer.MAX_VALUE,duration/1000));timeline.setEnabled(duration>0);if(!seeking)timeline.setProgress((int)(controller.getCurrentPosition()/1000));elapsed.setText(Track.time(controller.getCurrentPosition()/1000));total.setText(Track.time(duration/1000));}}
        else {miniTitle.setText("Dinlemeye hazırsın");miniArtist.setText("Bir parça seç, ritmini bul.");miniCover.setTag(null);miniCover.setImageDrawable(Ui.icon(this,"music",Ui.LIME));if(playerDialog!=null)playerDialog.dismiss();}
    }
    void showPlayer(){if(controller==null||controller.getCurrentMediaItem()==null){message("Önce bir parça seç.");return;}if(playerDialog!=null&&playerDialog.isShowing())return;playerDialog=new Dialog(this,R.style.AppTheme);LinearLayout shell=Ui.vertical(this);shell.setBackgroundColor(Ui.BG);playerDialog.setContentView(shell);Window window=playerDialog.getWindow();window.setLayout(-1,-1);WindowCompat.setDecorFitsSystemWindows(window,false);ViewCompat.setOnApplyWindowInsetsListener(shell,(v,insets)->{androidx.core.graphics.Insets a=insets.getInsets(WindowInsetsCompat.Type.systemBars()|WindowInsetsCompat.Type.displayCutout());shell.setPadding(a.left,a.top,a.right,a.bottom);return insets;});LinearLayout toolbar=Ui.horizontal(this);toolbar.setPadding(dp(12),dp(4),dp(12),dp(4));ImageButton down=Ui.action(this,"down","Oynatıcıyı küçült",false);down.setOnClickListener(v->playerDialog.dismiss());toolbar.addView(down,lp(48,48));TextView title=Ui.text(this,"ŞİMDİ ÇALIYOR",11,true,Ui.MUTED);title.setGravity(Gravity.CENTER);toolbar.addView(title,new LinearLayout.LayoutParams(0,-2,1));ImageButton more=Ui.action(this,"more","Parça seçenekleri",false);more.setOnClickListener(v->trackMenu(fromMedia(controller.getCurrentMediaItem()),controller.getCurrentMediaItemIndex()));toolbar.addView(more,lp(48,48));shell.addView(toolbar);
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);shell.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));LinearLayout controls=Ui.vertical(this);controls.setPadding(dp(28),dp(16),dp(28),dp(24));scroll.addView(controls,new ScrollView.LayoutParams(-1,-2));int coverHeight=Math.min(340,Math.max(170,getResources().getConfiguration().screenHeightDp/3));fullCover=new ImageView(this);fullCover.setScaleType(ImageView.ScaleType.CENTER_CROP);fullCover.setBackground(Ui.shape(this,Ui.SURFACE,24));fullCover.setClipToOutline(true);controls.addView(fullCover,lp(-1,coverHeight));Ui.space(controls,this,0,24);
        LinearLayout meta=Ui.horizontal(this);LinearLayout names=Ui.vertical(this);fullTitle=Ui.text(this,"",23,true,Ui.TEXT);fullTitle.setMaxLines(4);fullArtist=Ui.text(this,"",14,false,Ui.MUTED);fullArtist.setPadding(0,dp(10),0,0);names.addView(fullTitle);names.addView(fullArtist);meta.addView(names,new LinearLayout.LayoutParams(0,-2,1));fullLike=Ui.action(this,"heart","Beğen",false);fullLike.setOnClickListener(v->like(fromMedia(controller.getCurrentMediaItem())));meta.addView(fullLike,lp(48,48));controls.addView(meta);fullStatus=Ui.text(this,"",11,false,Ui.LIME);fullStatus.setPadding(0,dp(14),0,dp(4));controls.addView(fullStatus);
        timeline=new SeekBar(this);timeline.setProgressTintList(ColorStateList.valueOf(Ui.LIME));timeline.setThumbTintList(ColorStateList.valueOf(Ui.LIME));timeline.setContentDescription("Parçada ileri veya geri sar");timeline.setPadding(dp(12),0,dp(12),0);timeline.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar b,int p,boolean user){if(user)elapsed.setText(Track.time(p));}public void onStartTrackingTouch(SeekBar b){seeking=true;}public void onStopTrackingTouch(SeekBar b){controller.seekTo(b.getProgress()*1000L);seeking=false;}});controls.addView(timeline,lp(-1,48));LinearLayout times=Ui.horizontal(this);elapsed=Ui.text(this,"0:00",11,false,Ui.MUTED);total=Ui.text(this,"0:00",11,false,Ui.MUTED);total.setGravity(Gravity.END);times.addView(elapsed,new LinearLayout.LayoutParams(0,-2,1));times.addView(total,lp(-2,-2));controls.addView(times);
        LinearLayout transport=Ui.horizontal(this);transport.setGravity(Gravity.CENTER);transport.setPadding(0,dp(16),0,dp(16));fullShuffle=Ui.action(this,"shuffle","Karışık çalmayı aç veya kapat",false);fullShuffle.setOnClickListener(v->{controller.setShuffleModeEnabled(!controller.getShuffleModeEnabled());refreshPlayback();});transport.addView(fullShuffle,lp(48,48));ImageButton previous=Ui.action(this,"previous","Önceki parça",false);previous.setOnClickListener(v->controller.seekToPrevious());transport.addView(previous,new LinearLayout.LayoutParams(0,dp(56),1));fullPlay=Ui.action(this,"play","Çal veya duraklat",true);fullPlay.setOnClickListener(v->togglePlay());transport.addView(fullPlay,lp(72,72));ImageButton next=Ui.action(this,"next","Sonraki parça",false);next.setOnClickListener(v->skipNext());transport.addView(next,new LinearLayout.LayoutParams(0,dp(56),1));fullRepeat=Ui.action(this,"repeat","Tekrar modu",false);fullRepeat.setOnClickListener(v->{int current=controller.getRepeatMode();controller.setRepeatMode(current==Player.REPEAT_MODE_OFF?Player.REPEAT_MODE_ONE:current==Player.REPEAT_MODE_ONE?Player.REPEAT_MODE_ALL:Player.REPEAT_MODE_OFF);refreshPlayback();});transport.addView(fullRepeat,lp(48,48));controls.addView(transport,lp(-1,-2));
        LinearLayout extras=Ui.horizontal(this);Button add=Ui.button(this,"Playlist’e ekle",false);add.setOnClickListener(v->addToPlaylist(fromMedia(controller.getCurrentMediaItem())));extras.addView(add,new LinearLayout.LayoutParams(0,-2,1));Ui.space(extras,this,12,0);ImageButton stop=Ui.action(this,"stop","Çalmayı durdur",false);stop.setOnClickListener(v->{controller.pause();controller.stop();controller.seekTo(0);refreshPlayback();});extras.addView(stop,lp(48,48));ImageButton queue=Ui.action(this,"queue","Çalma sırasını görüntüle",false);queue.setOnClickListener(v->{playerDialog.dismiss();tab=3;opened=null;showPage(true);});extras.addView(queue,lp(48,48));controls.addView(extras);LinearLayout volume=Ui.horizontal(this);ImageView speaker=new ImageView(this);speaker.setImageDrawable(Ui.icon(this,"volume",Ui.MUTED));volume.addView(speaker,lp(24,24));SeekBar level=new SeekBar(this);level.setContentDescription("Uygulama ses seviyesi");level.setMax(100);level.setProgress((int)(controller.getVolume()*100));level.setProgressTintList(ColorStateList.valueOf(Ui.LIME));level.setThumbTintList(ColorStateList.valueOf(Ui.LIME));level.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar b,int p,boolean user){if(user)controller.setVolume(p/100f);}public void onStartTrackingTouch(SeekBar b){}public void onStopTrackingTouch(SeekBar b){}});volume.addView(level,new LinearLayout.LayoutParams(0,dp(48),1));controls.addView(volume);playerDialog.setOnDismissListener(d->{playerDialog=null;seeking=false;});playerDialog.show();window.setLayout(-1,-1);refreshPlayback();
    }
    @Override protected void onDestroy(){clearSuggestions();handler.removeCallbacksAndMessages(null);if(searchTask!=null)searchTask.cancel(true);if(playerDialog!=null)playerDialog.dismiss();if(controller!=null)controller.removeListener(listener);if(controllerFuture!=null)MediaController.releaseFuture(controllerFuture);super.onDestroy();}

    final class Rows extends RecyclerView.Adapter<Rows.Holder> {
        final ArrayList<Track> tracks=new ArrayList<>();final ArrayList<Library.Playlist> playlists=new ArrayList<>();boolean playlistPage;
        void refresh(){playlistPage=tab==2&&opened==null;tracks.clear();playlists.clear();if(playlistPage){if(app.library!=null)playlists.addAll(app.library.playlists);}else tracks.addAll(visibleTracks());notifyDataSetChanged();}
        public int getItemCount(){return playlistPage?playlists.size():tracks.size();}
        public Holder onCreateViewHolder(ViewGroup parent,int type){LinearLayout row=Ui.horizontal(MainActivity.this);row.setPadding(dp(8),dp(12),0,dp(12));row.setMinimumHeight(dp(88));row.setBackground(Ui.ripple(MainActivity.this,Color.TRANSPARENT,14));ImageView image=new ImageView(MainActivity.this);image.setScaleType(ImageView.ScaleType.CENTER_CROP);image.setBackground(Ui.shape(MainActivity.this,Ui.SURFACE,10));image.setClipToOutline(true);row.addView(image,lp(88,56));Ui.space(row,MainActivity.this,12,0);LinearLayout text=Ui.vertical(MainActivity.this);TextView title=Ui.text(MainActivity.this,"",14,true,Ui.TEXT);title.setMaxLines(2);title.setEllipsize(TextUtils.TruncateAt.END);TextView detail=Ui.text(MainActivity.this,"",12,false,Ui.MUTED);detail.setMaxLines(1);detail.setEllipsize(TextUtils.TruncateAt.END);detail.setPadding(0,dp(7),0,0);text.addView(title);text.addView(detail);row.addView(text,new LinearLayout.LayoutParams(0,-2,1));ImageButton menu=Ui.action(MainActivity.this,"more","Parça seçenekleri",false);row.addView(menu,lp(48,56));row.setLayoutParams(new RecyclerView.LayoutParams(-1,-2));return new Holder(row,image,title,detail,menu);}
        public void onBindViewHolder(Holder h,int index){
            if(playlistPage){
                Library.Playlist playlist=playlists.get(index);
                h.title.setText(playlist.name);h.detail.setText(playlist.tracks.size()+" parça");h.image.setTag(null);
                if(!playlist.tracks.isEmpty())app.covers.load(playlist.tracks.get(0).cover,h.image);
                else h.image.setImageDrawable(Ui.icon(MainActivity.this,"library",Ui.LIME));
                h.itemView.setOnClickListener(v->{opened=playlist;playlistFilter="";showPage(true);});
                h.menu.setContentDescription(playlist.name+", playlist seçenekleri");
                h.menu.setOnClickListener(v->playlistMenu(playlist));
            }else{
                Track track=tracks.get(index);
                h.title.setText(track.title);
                String prefix="";
                if(tab==3&&controller!=null&&index==controller.getCurrentMediaItemIndex())prefix="Seçili · ";
                h.detail.setText(prefix+track.artist+(track.seconds>0?" · "+Track.time(track.seconds):""));
                app.covers.load(track.cover,h.image);
                h.itemView.setOnClickListener(v->{
                    int i=h.getBindingAdapterPosition();
                    if(i==RecyclerView.NO_POSITION)return;
                    if(tab==3)playQueueItem(i);else playNow(track);
                });
                h.menu.setContentDescription(track.title+", parça seçenekleri");
                h.menu.setOnClickListener(v->{int i=h.getBindingAdapterPosition();if(i!=RecyclerView.NO_POSITION)trackMenu(track,i);});
            }
            h.image.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        }
        final class Holder extends RecyclerView.ViewHolder {final ImageView image;final TextView title,detail;final ImageButton menu;Holder(View v,ImageView i,TextView t,TextView d,ImageButton m){super(v);image=i;title=t;detail=d;menu=m;}}
    }
}
