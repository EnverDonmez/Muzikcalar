package com.muzikcalar.app;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Handler;
import android.os.Looper;
import android.util.LruCache;
import android.widget.ImageView;
import java.io.IOException;
import java.util.concurrent.Executor;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.Response;

final class CoverLoader {
    private final OkHttpClient http;
    private final Executor io;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final LruCache<String, Bitmap> covers = new LruCache<>(16 * 1024 * 1024) {
        @Override protected int sizeOf(String key, Bitmap image) { return image.getAllocationByteCount(); }
    };

    CoverLoader(OkHttpClient http, Executor io) {
        this.http = http;
        this.io = io;
    }

    void load(String url, ImageView view) {
        view.setTag(url);
        Bitmap cached = covers.get(url);
        if (cached != null) {
            view.setImageBitmap(cached);
            return;
        }
        view.setImageDrawable(Ui.icon(view.getContext(), "music", Ui.MUTED));
        if (!url.startsWith("https://")) return;
        io.execute(() -> {
            try {
                Bitmap image = fetch(url);
                if (image != null) main.post(() -> {
                    if (url.equals(view.getTag())) view.setImageBitmap(image);
                });
            } catch (IOException | IllegalArgumentException ignored) { }
        });
    }

    private Bitmap fetch(String url) throws IOException {
        Bitmap cached = covers.get(url);
        if (cached != null) return cached;
        try (Response response = http.newCall(new Request.Builder().url(url).build()).execute()) {
            if (!response.isSuccessful() || response.body() == null) return null;
            byte[] bytes = response.body().bytes();
            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inJustDecodeBounds = true;
            BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
            options.inSampleSize = Math.max(1, Math.max(options.outWidth, options.outHeight) / 640);
            options.inJustDecodeBounds = false;
            Bitmap image = BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
            if (image != null) covers.put(url, image);
            return image;
        }
    }
}
