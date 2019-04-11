using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ReferenceSearchWindow : EditorWindow
{
    [MenuItem("Window/Reference Search %#r")]
    static void showWindow()
    {
        var window = GetWindow<ReferenceSearchWindow>();
    }

    /// <summary>
    /// このオブジェクトを参照しているオブジェクトを検索する
    /// </summary>
    Object searchObject;

    /// <summary>
    /// searchObject への参照を置き換える参照
    /// </summary>
    Object replaceReference;
    Vector2 scrollPosition;

    /// <summary>
    ///  検索にヒットしたゲームオブジェクトのリスト
    /// </summary>
    List<GameObject> hitGameObjects = new List<GameObject>();

    /// <summary>
    /// エディタの状態追跡用
    /// </summary>
    ActiveEditorTracker tracker = new ActiveEditorTracker();

    void OnEnable()
    {
        // ウインドウタイトル変更
        titleContent = new GUIContent("Reference Search");

        EditorApplication.hierarchyChanged += () => {
            updateSearch();
        };
    }

    void OnInspectorUpdate()
    {
        if (!!tracker.isDirty) {
            updateSearch();
            Repaint();
        }
    }

    void OnGUI()
    {
        switch (Event.current.type) {
        case EventType.Repaint:
            tracker.ClearDirty();
            break;
        }

        searchObject = EditorGUILayout.ObjectField("Search Object", searchObject, typeof(Object), true);

        replaceReference = EditorGUILayout.ObjectField("Replace Reference", replaceReference, typeof(Object), true);

        if (GUILayout.Button("Replace")) {
            var gameObjects = FindObjectsOfType<GameObject>();
            foreach (var gameObject in gameObjects) {
                foreach (var component in gameObject.GetComponents<MonoBehaviour>()) {
                    var serializedObject = new SerializedObject(component);
                    var iterator = serializedObject.GetIterator();
                    iterator.Next(true);
                    while (iterator.Next(true)) {
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
                            if (iterator.objectReferenceValue == searchObject &&
                                iterator.objectReferenceValue != gameObject) {
                                iterator.objectReferenceValue = replaceReference;
                            }
                        }
                    }
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
        drawSeparator();

        using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPosition)) {
            if (!!searchObject) {

                foreach (var hitGameObject in hitGameObjects) {
                    var content = EditorGUIUtility.ObjectContent(hitGameObject, typeof(GameObject));
                    if (GUILayout.Button(content, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight))) {
                        EditorGUIUtility.PingObject(hitGameObject);
                        Selection.activeGameObject = hitGameObject;
                    }
                }
            }

            scrollPosition = scrollViewScope.scrollPosition;
        }
    }

    void updateSearch()
    {
        var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        hitGameObjects.Clear();
        foreach (var gameObject in gameObjects) {
            foreach (var component in gameObject.GetComponents<MonoBehaviour>()) {
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                iterator.Next(true);
                while (iterator.Next(true)) {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
                        if (gameObject.scene.isLoaded &&
							iterator.objectReferenceValue == searchObject &&
                            iterator.objectReferenceValue != gameObject) {
                            hitGameObjects.Add(gameObject);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 区切り線の描画
    /// </summary>
    void drawSeparator()
    {
        var lineStyle = new GUIStyle("box");
        lineStyle.border.top = lineStyle.border.bottom = 1;
        lineStyle.margin.top = lineStyle.margin.bottom = 1;
        lineStyle.padding.top = lineStyle.padding.bottom = 1;
        lineStyle.margin.left = lineStyle.margin.right = 0;
        lineStyle.padding.left = lineStyle.padding.right = 0;
        GUILayout.Box(GUIContent.none, lineStyle, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
    }
}
