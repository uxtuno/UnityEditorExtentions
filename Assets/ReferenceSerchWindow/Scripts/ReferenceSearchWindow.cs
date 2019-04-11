using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.Animations;

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
    Object referencedObject;

    /// <summary>
    /// searchObject への参照を置き換える参照
    /// </summary>
    Object replaceReference;
    Vector2 scrollPosition;

	class SearchHitInfo
	{
		public GameObject hitGameObject;
		public Object referObject;
		public string hitProperty;
		public string hitPropertyPath;
	}

    /// <summary>
    ///  検索にヒットしたゲームオブジェクトのリスト
    /// </summary>
    List<SearchHitInfo> hitInfoList = new List<SearchHitInfo>();

    /// <summary>
    /// エディタの状態追跡用
    /// </summary>
    ActiveEditorTracker tracker = new ActiveEditorTracker();

	/// <summary>
	/// searchObject に指定したGameObjectのコンポーネントを含めて検索する
	/// </summary>
	bool includeComponent = true;

    void OnEnable()
    {
        // ウインドウタイトル変更
        titleContent = new GUIContent("Reference Search");
		updateSearch();
    }

    void OnInspectorUpdate()
    {
        if (!!tracker.isDirty) {
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

		using (var changeScope = new EditorGUI.ChangeCheckScope()) {
			referencedObject = EditorGUILayout.ObjectField("Referenced Object", referencedObject, typeof(Object), true);
			replaceReference = EditorGUILayout.ObjectField("Replace Reference", replaceReference, typeof(Object), true);
			includeComponent = EditorGUILayout.Toggle("Include Component", includeComponent);

			if (!!changeScope.changed) {
				updateSearch();
			}
		}

		if (GUILayout.Button("Replace")) {
			var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
			foreach (var gameObject in gameObjects) {
                foreach (var component in gameObject.GetComponents<Component>()) {
                    var serializedObject = new SerializedObject(component);
                    var iterator = serializedObject.GetIterator();
                    iterator.Next(true);
                    while (iterator.Next(true)) {
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
							var isComponentHitted =
							((referencedObject is GameObject) &&
							!!includeComponent &&
							((GameObject)referencedObject).GetComponents<Component>().Any(item => item == iterator.objectReferenceValue));

							if ((iterator.objectReferenceValue == referencedObject || !!isComponentHitted) &&
								iterator.objectReferenceValue != gameObject) {
								Debug.Log($"Replace {iterator.propertyPath} {iterator.objectReferenceValue} -> {replaceReference}");
                                iterator.objectReferenceValue = replaceReference;
                            }
                        }
                    }
                    serializedObject.ApplyModifiedProperties();
                }
            }

			updateSearch();
		}
        drawSeparator();

        using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPosition)) {
            if (!!referencedObject) {

                foreach (var hitInfo in hitInfoList) {
                    var content = EditorGUIUtility.ObjectContent(hitInfo.hitGameObject, typeof(GameObject));
					content.text += " (" + hitInfo.hitPropertyPath + ")";

					using (var horizontal = new EditorGUILayout.HorizontalScope()) {
						if (GUILayout.Button(content, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(Screen.width / 2.0f - 6.0f))) {
							EditorGUIUtility.PingObject(hitInfo.hitGameObject);
							Selection.activeGameObject = hitInfo.hitGameObject;
						}

						EditorGUILayout.TextArea(hitInfo.hitProperty, GUILayout.Width(Screen.width / 2.0f - 6.0f));
					}
				}
            }

            scrollPosition = scrollViewScope.scrollPosition;
        }

		if (GUILayout.Button("Refresh")) {
			updateSearch();
		}
	}

    void updateSearch()
    {
        var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        hitInfoList.Clear();
        foreach (var gameObject in gameObjects) {
            foreach (var component in gameObject.GetComponents<Component>()) {
				if (!component) {
					continue;
				}
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                iterator.Next(true);
                while (iterator.Next(true)) {
					if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
						// GameObjectの場合は、アタッチされているコンポーネントも検索対象に含める
						var isComponentHitted =
							((referencedObject is GameObject) &&
							!!includeComponent && 
							((GameObject)referencedObject).GetComponents<Component>().Any(item => item == iterator.objectReferenceValue));

                        if ((!!gameObject && gameObject.scene != null && gameObject.scene.isLoaded) &&
							(iterator.objectReferenceValue == referencedObject || !!isComponentHitted) &&
							iterator.objectReferenceValue != gameObject) {
							var hitInfo = new SearchHitInfo();
							hitInfo.hitGameObject = gameObject;
							hitInfo.referObject = iterator.objectReferenceValue;
							hitInfo.hitProperty =  iterator.name;
							hitInfo.hitPropertyPath =  string.Format($"{iterator.serializedObject.targetObject.GetType()}");
							hitInfoList.Add(hitInfo);
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
