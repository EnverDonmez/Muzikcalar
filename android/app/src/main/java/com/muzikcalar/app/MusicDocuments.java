package com.muzikcalar.app;

import android.content.ClipData;
import android.content.ContentResolver;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.provider.OpenableColumns;
import android.util.Log;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;

final class MusicDocuments {
    private static final int MAX_IMPORT_BYTES = 8 * 1024 * 1024;

    static String readLibrary(ContentResolver resolver, Uri uri) throws IOException {
        try (InputStream input = resolver.openInputStream(uri);
                ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            if (input == null) throw new IOException("Dosya açılamadı.");
            byte[] buffer = new byte[8192];
            int count;
            while ((count = input.read(buffer)) != -1) {
                if (output.size() + count > MAX_IMPORT_BYTES)
                    throw new IOException("Kitaplık dosyası çok büyük.");
                output.write(buffer, 0, count);
            }
            return new String(output.toByteArray(), StandardCharsets.UTF_8);
        }
    }

    static void writeLibrary(ContentResolver resolver, Uri uri, String snapshot) throws IOException {
        try (OutputStream output = resolver.openOutputStream(uri, "wt")) {
            if (output == null) throw new IOException("Dosya açılamadı.");
            output.write(snapshot.getBytes(StandardCharsets.UTF_8));
        }
    }

    static List<Track> readAudioSelection(ContentResolver resolver, Intent selection) {
        LinkedHashSet<Uri> uris = new LinkedHashSet<>();
        if (selection.getData() != null) uris.add(selection.getData());
        ClipData items = selection.getClipData();
        if (items != null) {
            for (int i = 0; i < items.getItemCount(); i++) uris.add(items.getItemAt(i).getUri());
        }
        List<Track> tracks = new ArrayList<>();
        for (Uri uri : uris) {
            if (uri == null || !"content".equals(uri.getScheme())) continue;
            try {
                resolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
                String title = "Yerel parça";
                try (Cursor cursor = resolver.query(uri, new String[]{OpenableColumns.DISPLAY_NAME}, null, null, null)) {
                    if (cursor != null && cursor.moveToFirst() && cursor.getString(0) != null)
                        title = cursor.getString(0);
                }
                tracks.add(Track.local(uri, title));
            } catch (RuntimeException e) {
                Log.w("Muzikcalar", "Yerel dosya açılamadı");
            }
        }
        return tracks;
    }
}
