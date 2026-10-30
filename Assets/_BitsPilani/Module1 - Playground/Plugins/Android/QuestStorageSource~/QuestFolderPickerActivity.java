package com.bitspilani.storage;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;

public final class QuestFolderPickerActivity extends Activity {
    String receiver, root;
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        receiver = getIntent().getStringExtra("receiver"); root = getIntent().getStringExtra("root");
        if (state != null) return;
        try {
            Intent picker = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
            picker.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION | Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
            picker.putExtra(DocumentsContract.EXTRA_INITIAL_URI, DocumentsContract.buildDocumentUri("com.android.externalstorage.documents", "primary:" + root));
            startActivityForResult(picker, 731);
        } catch (Exception ex) { QuestStorageBridge.send(receiver, "error|A system folder picker is unavailable: " + ex.getMessage()); finish(); }
    }
    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != 731) return;
        try {
            if (result != RESULT_OK || data == null || data.getData() == null) throw new Exception("Folder selection was cancelled.");
            Uri uri = data.getData();
            if (!DocumentsContract.getTreeDocumentId(uri).equals("primary:" + root)) throw new Exception("Choose the " + root + " folder directly inside Internal shared storage.");
            int flags = data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            if ((flags & Intent.FLAG_GRANT_WRITE_URI_PERMISSION) == 0) throw new Exception("The selected folder did not grant write access.");
            getContentResolver().takePersistableUriPermission(uri, flags);
            getSharedPreferences(QuestStorageBridge.PREFS, 0).edit().putString("tree_" + root, uri.toString()).apply();
            QuestStorageBridge.send(receiver, "ok|" + uri.toString());
        } catch (Exception ex) { QuestStorageBridge.send(receiver, "error|" + ex.getMessage()); }
        finish();
    }
}
