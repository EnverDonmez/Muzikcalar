package com.muzikcalar.app;
import android.content.*;
import android.content.res.ColorStateList;
import android.graphics.*;
import android.graphics.drawable.*;
import android.view.*;
import android.widget.*;
import androidx.core.graphics.PathParser;
import java.util.*;

final class Ui {
    static final int BG=Color.rgb(13,17,23),SURFACE=Color.rgb(24,30,39),RAISED=Color.rgb(34,42,53),TEXT=Color.rgb(245,247,250),MUTED=Color.rgb(165,179,195),LIME=Color.rgb(200,245,111),INK=Color.rgb(18,28,17),DANGER=Color.rgb(255,145,145);
    static Typeface regular,bold;
    static void fonts(Context c){if(regular==null){regular=Typeface.createFromAsset(c.getAssets(),"Montserrat-Regular.ttf");bold=Typeface.createFromAsset(c.getAssets(),"Montserrat-ExtraBold.ttf");}}
    static int dp(Context c,float n){return Math.round(n*c.getResources().getDisplayMetrics().density);}
    static GradientDrawable shape(Context c,int color,int radius){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(c,radius));return d;}
    static Drawable ripple(Context c,int color,int radius){return new RippleDrawable(ColorStateList.valueOf(0x33C8F56F),shape(c,color,radius),shape(c,Color.WHITE,radius));}
    static TextView text(Context c,String value,int sp,boolean heavy,int color){fonts(c);TextView t=new TextView(c);t.setText(value);t.setTextSize(sp);t.setTextColor(color);t.setTypeface(heavy?bold:regular);t.setIncludeFontPadding(false);t.setGravity(Gravity.CENTER_VERTICAL);return t;}
    static Button button(Context c,String title,boolean primary){fonts(c);Button b=new Button(c);b.setText(title);b.setAllCaps(false);b.setTextSize(14);b.setTypeface(bold);b.setTextColor(primary?INK:TEXT);b.setMinHeight(dp(c,48));b.setMinimumHeight(dp(c,48));b.setPadding(dp(c,16),dp(c,8),dp(c,16),dp(c,8));b.setBackground(ripple(c,primary?LIME:SURFACE,16));return b;}
    static ImageButton action(Context c,String name,String label,boolean primary){ImageButton b=new ImageButton(c);b.setImageDrawable(icon(c,name,primary?INK:TEXT));b.setContentDescription(label);b.setPadding(dp(c,12),dp(c,12),dp(c,12),dp(c,12));b.setScaleType(ImageView.ScaleType.FIT_CENTER);b.setBackground(ripple(c,primary?LIME:Color.TRANSPARENT,18));b.setMinimumWidth(dp(c,48));b.setMinimumHeight(dp(c,48));return b;}
    static void space(LinearLayout row,Context c,int width,int height){View v=new View(c);row.addView(v,new LinearLayout.LayoutParams(dp(c,width),dp(c,height)));}
    static LinearLayout vertical(Context c){LinearLayout l=new LinearLayout(c);l.setOrientation(LinearLayout.VERTICAL);return l;}
    static LinearLayout horizontal(Context c){LinearLayout l=new LinearLayout(c);l.setGravity(Gravity.CENTER_VERTICAL);return l;}
    static Drawable icon(Context context,String name,int color){return new Symbol(context,name,color);}
    static final Map<String,String> PATHS=new HashMap<>();
    static {
        PATHS.put("search","M21 21l-4.5-4.5 M19 10.5a8.5 8.5 0 1 1-17 0 8.5 8.5 0 0 1 17 0");
        PATHS.put("heart","M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8");
        PATHS.put("library","M4 4v16 M8 4v16 M12 4v16 M16 4l4 16");
        PATHS.put("queue","M4 6h16 M4 12h16 M4 18h11");
        PATHS.put("more","M12 5h.01 M12 12h.01 M12 19h.01");
        PATHS.put("play","M8 4l13 8L8 20Z");
        PATHS.put("pause","M7 5h3v14H7Z M14 5h3v14h-3Z");
        PATHS.put("next","M5 5l11 7L5 19Z M19 5v14");
        PATHS.put("previous","M19 5L8 12l11 7Z M5 5v14");
        PATHS.put("repeat","M17 2l4 4-4 4 M3 11V9a3 3 0 0 1 3-3h15 M7 22l-4-4 4-4 M21 13v2a3 3 0 0 1-3 3H3");
        PATHS.put("stop","M6 6h12v12H6Z");
        PATHS.put("plus","M12 5v14 M5 12h14");
        PATHS.put("back","M15 18l-6-6 6-6");
        PATHS.put("down","M6 9l6 6 6-6");
        PATHS.put("close","M6 6l12 12 M18 6L6 18");
        PATHS.put("settings","M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8 M12 2v3 M12 19v3 M2 12h3 M19 12h3 M5 5l2 2 M17 17l2 2 M5 19l2-2 M17 7l2-2");
        PATHS.put("music","M9 18V5l12-2v13 M9 8l12-2 M9 18a3 3 0 1 1-3-3c2 0 3 1 3 3 M21 16a3 3 0 1 1-3-3c2 0 3 1 3 3");
        PATHS.put("history","M3 11a9 9 0 1 1 2 7 M3 5v6h6 M12 7v5l3 2");
        PATHS.put("volume","M11 5L6 9H3v6h3l5 4Z M15 8a6 6 0 0 1 0 8 M18 5a10 10 0 0 1 0 14");
        PATHS.put("shuffle","M3 6h3l12 12h3 M17 14l4 4-4 4 M3 18h3l4-4 M14 10l4-4h3 M17 2l4 4-4 4");
    }
    static final class Symbol extends Drawable {
        final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);final Path path;final int intrinsic;final boolean fill;
        Symbol(Context c,String name,int color){path=PathParser.createPathFromPathData(PATHS.getOrDefault(name,PATHS.get("music")));paint.setColor(color);paint.setStrokeWidth(1.8f);paint.setStrokeCap(Paint.Cap.ROUND);paint.setStrokeJoin(Paint.Join.ROUND);fill=name.equals("play")||name.equals("pause")||name.equals("stop");paint.setStyle(fill?Paint.Style.FILL:Paint.Style.STROKE);intrinsic=dp(c,24);}
        public void draw(Canvas canvas){Rect b=getBounds();canvas.save();float scale=Math.min(b.width(),b.height())/24f;canvas.translate(b.left+(b.width()-scale*24)/2,b.top+(b.height()-scale*24)/2);canvas.scale(scale,scale);canvas.drawPath(path,paint);canvas.restore();}
        public void setAlpha(int a){paint.setAlpha(a);invalidateSelf();}public void setColorFilter(ColorFilter f){paint.setColorFilter(f);invalidateSelf();}public int getOpacity(){return PixelFormat.TRANSLUCENT;}public int getIntrinsicWidth(){return intrinsic;}public int getIntrinsicHeight(){return intrinsic;}
    }
}
