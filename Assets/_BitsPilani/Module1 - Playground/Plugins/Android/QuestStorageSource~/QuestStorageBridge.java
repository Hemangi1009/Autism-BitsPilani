package com.bitspilani.storage;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.ContentResolver;
import android.content.UriPermission;
import android.database.Cursor;
import android.net.Uri;
import android.provider.DocumentsContract;
import java.io.*;
import java.security.MessageDigest;

public final class QuestStorageBridge {
    static final String PREFS = "bits_pilani_recordings";
    public static void select(Activity activity, String receiver, String root) {
        Intent intent = new Intent(activity, QuestFolderPickerActivity.class);
        intent.putExtra("receiver", receiver); intent.putExtra("root", root);
        activity.startActivity(intent);
    }
    public static String getTree(Context context, String root) {
        String value = context.getSharedPreferences(PREFS, 0).getString("tree_" + root, "");
        if (value.isEmpty()) return "";
        try {
            Uri uri = Uri.parse(value);
            if (!DocumentsContract.getTreeDocumentId(uri).equals("primary:" + root)) return "";
            for (UriPermission permission : context.getContentResolver().getPersistedUriPermissions())
                if (permission.getUri().equals(uri) && permission.isWritePermission()) return value;
        } catch (Exception ignored) { }
        return "";
    }
    static Uri child(ContentResolver resolver, Uri tree, Uri parent, String name) {
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, DocumentsContract.getDocumentId(parent));
        try (Cursor cursor = resolver.query(children, new String[]{DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME}, null, null, null)) {
            if (cursor != null) while (cursor.moveToNext()) if (name.equals(cursor.getString(1))) return DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0));
        }
        return null;
    }
    static byte[] digest(InputStream input) throws Exception {
        MessageDigest hash = MessageDigest.getInstance("SHA-256");
        byte[] buffer = new byte[65536]; int count;
        while ((count = input.read(buffer)) != -1) hash.update(buffer, 0, count);
        return hash.digest();
    }
    static boolean same(ContentResolver resolver, Uri uri, File source) throws Exception {
        try (InputStream existing = resolver.openInputStream(uri); InputStream local = new FileInputStream(source)) {
            return java.util.Arrays.equals(digest(existing), digest(local));
        }
    }
    public static String publish(Context context, String root, String subfolder, String sourcePath) throws Exception {
        String saved = getTree(context, root);
        if (saved.isEmpty()) throw new IOException("Select the internal-storage " + root + " folder before starting recording.");
        ContentResolver resolver = context.getContentResolver();
        Uri tree = Uri.parse(saved);
        Uri directory = DocumentsContract.buildDocumentUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
        Uri target = child(resolver, tree, directory, subfolder);
        if (target == null) target = DocumentsContract.createDocument(resolver, directory, DocumentsContract.Document.MIME_TYPE_DIR, subfolder);
        if (target == null) throw new IOException("Could not create " + root + "/" + subfolder);
        File source = new File(sourcePath);
        Uri existing = child(resolver, tree, target, source.getName());
        if (existing != null) {
            if (same(resolver, existing, source)) return existing.toString();
            throw new IOException("A different video already has this filename; it was not overwritten.");
        }
        String tempName = source.getName().replaceFirst("\\.mp4$", "") + ".saving.mp4";
        Uri interrupted = child(resolver, tree, target, tempName);
        if (interrupted != null) {
            if (same(resolver, interrupted, source)) {
                Uri recovered = DocumentsContract.renameDocument(resolver, interrupted, source.getName());
                if (recovered != null) return recovered.toString();
            }
            DocumentsContract.deleteDocument(resolver, interrupted);
        }
        Uri pending = DocumentsContract.createDocument(resolver, target, "video/mp4", tempName);
        if (pending == null) throw new IOException("Could not create the Quest video.");
        try {
            try (InputStream input = new FileInputStream(source); OutputStream output = resolver.openOutputStream(pending, "w")) {
                if (output == null) throw new IOException("Could not open the Quest video for writing.");
                byte[] buffer = new byte[65536]; int count;
                while ((count = input.read(buffer)) != -1) output.write(buffer, 0, count);
                output.flush();
            }
            if (!same(resolver, pending, source)) throw new IOException("Quest video verification failed. The local source was retained.");
            Uri finished = DocumentsContract.renameDocument(resolver, pending, source.getName());
            if (finished == null) throw new IOException("Could not finalize the video filename.");
            return finished.toString();
        } catch (Exception ex) { try { DocumentsContract.deleteDocument(resolver, pending); } catch (Exception ignored) { } throw ex; }
    }
    static void send(String receiver, String message) {
        try { Class.forName("com.unity3d.player.UnityPlayer").getMethod("UnitySendMessage", String.class, String.class, String.class).invoke(null, receiver, "OnFolderPickerResult", message); } catch (Exception ignored) { }
    }
}
