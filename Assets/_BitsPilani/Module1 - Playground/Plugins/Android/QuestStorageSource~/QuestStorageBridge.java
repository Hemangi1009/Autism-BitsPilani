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
    static Uri ensurePath(ContentResolver resolver, Uri tree, Uri parent, String path) throws Exception {
        if (path == null || path.isEmpty()) return parent;
        for (String name : path.split("/")) {
            if (name.isEmpty() || name.equals(".") || name.equals("..") || !name.matches("[A-Za-z0-9 &_\\-]+")) throw new IOException("Invalid directory name.");
            Uri next = child(resolver, tree, parent, name);
            if (next == null) next = DocumentsContract.createDocument(resolver, parent, DocumentsContract.Document.MIME_TYPE_DIR, name);
            if (next == null) throw new IOException("Could not create " + name);
            parent = next;
        }
        return parent;
    }
    private static String prepareTree(Context context, String root, String relative) throws Exception {
        String saved = getTree(context, root);
        if (saved.isEmpty()) throw new IOException("Select the internal-storage " + root + " folder before starting recording.");
        ContentResolver resolver = context.getContentResolver();
        Uri tree = Uri.parse(saved);
        Uri directory = DocumentsContract.buildDocumentUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
        for (int module=1; module<=4; module++) ensurePath(resolver, tree, directory, "Module" + module);
        return ensurePath(resolver, tree, directory, relative).toString();
    }

    private static File makeDirectory(File parent, String name) throws Exception {
        if (name == null || !name.matches("[A-Za-z0-9][A-Za-z0-9 &_\\-]{0,63}")) throw new IOException("Invalid recording directory name.");
        File directory = new File(parent,name).getCanonicalFile();
        if (!directory.getParentFile().equals(parent.getCanonicalFile())) throw new IOException("Recording directory escapes its parent.");
        if (!directory.isDirectory() && !directory.mkdirs() && !directory.isDirectory()) throw new IOException("Cannot create requested shared-storage directory: " + directory);
        return directory;
    }
    public static synchronized String prepare(Context context, String root, String relative) throws Exception {
        try {
            File directory=makeDirectory(android.os.Environment.getExternalStorageDirectory(),root);
            File probe=new File(directory,".bitspilani-write-check-"+java.util.UUID.randomUUID());
            try { try(FileOutputStream output=new FileOutputStream(probe)) {output.write(1);} }
            finally { if(probe.exists())probe.delete(); }
            for(int i=1;i<=4;i++)makeDirectory(directory,"Module"+i);
            if(relative!=null&&!relative.isEmpty())for(String segment:relative.split("/"))directory=makeDirectory(directory,segment);
            return directory.toURI().toString();
        } catch(Exception denied) {
            if(!getTree(context,root).isEmpty())return prepareTree(context,root,relative);
            throw new IOException("The requested internal-storage folder is not writable by this app. No permission screen was opened. " + denied.getMessage(),denied);
        }
    }
    private static boolean sameFile(File a,File b)throws Exception {
        try(InputStream first=new FileInputStream(a);InputStream second=new FileInputStream(b)){return java.util.Arrays.equals(digest(first),digest(second));}
    }
    private static String publishDirect(Context context,File directory,File source,String name)throws Exception {
        if(name==null||name.isEmpty())name=source.getName();
        if(!name.matches("[A-Za-z0-9_.:\\-]+\\.mp4"))throw new IOException("Invalid video filename.");
        File destination=new File(directory,name),compatible=new File(directory,name.replace(':','-'));
        for(File existing:new File[]{destination,compatible})if(existing.exists()){
            if(sameFile(existing,source))return existing.getAbsolutePath()+"\n"+existing.getName();
            throw new IOException("An existing video differs from this session; it was not replaced.");
        }
        File pending=new File(directory,source.getName()+".saving");
        try(InputStream input=new FileInputStream(source);FileOutputStream output=new FileOutputStream(pending)){
            byte[] buffer=new byte[65536];int count;while((count=input.read(buffer))!=-1)output.write(buffer,0,count);output.getFD().sync();
        }
        if(!sameFile(source,pending))throw new IOException("Shared-storage checksum verification failed; source retained.");
        try{java.nio.file.Files.move(pending.toPath(),destination.toPath());}
        catch(IOException rejected){if(!name.contains(":"))throw rejected;java.nio.file.Files.move(pending.toPath(),compatible.toPath());destination=compatible;}
        try { android.media.MediaScannerConnection.scanFile(context,new String[]{destination.getAbsolutePath()},new String[]{"video/mp4"},null); } catch(Exception ignored) { }
        return destination.getAbsolutePath()+"\n"+destination.getName();
    }
    static String displayName(ContentResolver resolver, Uri uri) {
        try (Cursor cursor = resolver.query(uri, new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME}, null, null, null)) { if (cursor != null && cursor.moveToFirst()) return cursor.getString(0); }
        return "";
    }
    static String finishName(ContentResolver resolver, Uri pending, String requested) throws Exception {
        Uri uri = pending;
        try {
            Uri renamed = DocumentsContract.renameDocument(resolver, uri, requested);
            if (renamed != null) uri = renamed;
        } catch (Exception rejected) {
            if (requested.indexOf(':') < 0) throw rejected;
        }
        if (!requested.equals(displayName(resolver, uri))) {
            String compatible = requested.replace(':', '-');
            Uri compatibleUri = DocumentsContract.renameDocument(resolver, uri, compatible);
            if (compatibleUri != null) uri = compatibleUri;
            if (!compatible.equals(displayName(resolver, uri))) throw new IOException("Storage provider rejected the final video filename.");
        }
        return uri.toString() + "\n" + displayName(resolver, uri);
    }
    public static synchronized String publish(Context context, String root, String subfolder, String sourcePath, String requestedName) throws Exception {
        String prepared=prepare(context,root,subfolder);
        Uri target=Uri.parse(prepared);
        if("file".equals(target.getScheme()))return publishDirect(context,new File(java.net.URI.create(prepared)),new File(sourcePath),requestedName);
        Uri tree = Uri.parse(getTree(context, root));
        ContentResolver resolver = context.getContentResolver();
        File source = new File(sourcePath);
        if (requestedName == null || requestedName.isEmpty()) requestedName = source.getName();
        if (!requestedName.matches("[A-Za-z0-9_.:\\-]+\\.mp4")) throw new IOException("Invalid video filename.");
        Uri existing = child(resolver, tree, target, requestedName);
        if (existing == null) existing = child(resolver, tree, target, requestedName.replace(':', '-'));
        if (existing != null) {
            if (same(resolver, existing, source)) return existing.toString() + "\n" + displayName(resolver, existing);
            throw new IOException("A different video already has this filename; it was not overwritten.");
        }
        String tempName = source.getName().replaceFirst("\\.mp4$", "") + ".saving.mp4";
        Uri interrupted = child(resolver, tree, target, tempName);
        if (interrupted != null) {
            if (same(resolver, interrupted, source)) {
                return finishName(resolver, interrupted, requestedName);
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
            return finishName(resolver, pending, requestedName);
        } catch (Exception ex) { try { DocumentsContract.deleteDocument(resolver, pending); } catch (Exception ignored) { } throw ex; }
    }
    static void send(String receiver, String message) {
        try { Class.forName("com.unity3d.player.UnityPlayer").getMethod("UnitySendMessage", String.class, String.class, String.class).invoke(null, receiver, "OnFolderPickerResult", message); } catch (Exception ignored) { }
    }
}
