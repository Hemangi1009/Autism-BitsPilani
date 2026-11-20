using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach to any GameObject. Fill "Find Text" and "Replace With" in the Inspector,
/// then click the 3 dots (top-right of this component) > "Replace Name Part".
/// Renames this object (optional) and ALL children (including inactive ones).
/// </summary>
public class RenameChildrenReplace : MonoBehaviour
{
    [Header("Text to find inside the names")]
    public string findText = "name";

    [Header("Text to put instead")]
    public string replaceText = "newName";

    [Header("Options")]
    public bool includeSelf = true;
    public bool caseSensitive = true;

    [ContextMenu("Replace Name Part")]
    private void ReplaceNamePart()
    {
        if (string.IsNullOrEmpty(findText))
        {
            Debug.LogWarning("[RenameChildrenReplace] 'Find Text' is empty.", this);
            return;
        }

        // true = also include inactive (disabled) children
        Transform[] all = GetComponentsInChildren<Transform>(true);
        int changedCount = 0;

        foreach (Transform t in all)
        {
            if (!includeSelf && t == transform) continue;

            string oldName = t.name;
            string newName = ReplaceInString(oldName);

            if (newName == oldName) continue;

#if UNITY_EDITOR
            Undo.RecordObject(t.gameObject, "Replace Name Part");
#endif
            t.name = newName;
            changedCount++;

#if UNITY_EDITOR
            EditorUtility.SetDirty(t.gameObject);
#endif
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && changedCount > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        Debug.Log($"[RenameChildrenReplace] Renamed {changedCount} object(s).", this);
    }

    private string ReplaceInString(string input)
    {
        if (caseSensitive)
            return input.Replace(findText, replaceText);

        return Regex.Replace(
            input,
            Regex.Escape(findText),
            replaceText.Replace("$", "$$"),
            RegexOptions.IgnoreCase);
    }
}
