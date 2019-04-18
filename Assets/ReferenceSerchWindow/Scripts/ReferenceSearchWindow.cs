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
		public SerializedProperty property;
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
	bool includeSubObject = true;

	/// <summary>
	/// 非表示のプロパティを含むか
	/// </summary>
	bool includeInvisibleProperties = false;

	Dictionary<Object, List<SerializedProperty>> referenceMap;

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

		EditorGUIUtility.labelWidth = Screen.width / 2.0f - 8.0f;

		using (var changeScope = new EditorGUI.ChangeCheckScope()) {
			referencedObject = EditorGUILayout.ObjectField("Referenced Object", referencedObject, typeof(Object), true, GUILayout.ExpandWidth(true));
			replaceReference = EditorGUILayout.ObjectField("Replace Reference", replaceReference, typeof(Object), true, GUILayout.ExpandWidth(true));
			includeSubObject = EditorGUILayout.Toggle("Include Component", includeSubObject);
			includeInvisibleProperties = EditorGUILayout.Toggle("Include Invisible Properties", includeInvisibleProperties);

			if (!!changeScope.changed) {
				updateSearch();
			}
		}

		// 置換は、安全のため表示プロパティのみに有効
		if (!includeInvisibleProperties && GUILayout.Button("Replace")) {
			var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
			foreach (var gameObject in gameObjects) {
				foreach (var component in gameObject.GetComponents<Component>()) {
					var serializedObject = new SerializedObject(component);
					var iterator = serializedObject.GetIterator();
					iterator.NextVisible(true);
					while (iterator.NextVisible(true)) {
						if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
							var isComponentHitted =
							((referencedObject is GameObject) &&
							!!includeSubObject &&
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

				foreach (var hitGroup in hitInfoList.GroupBy(hitInfo => hitInfo.hitGameObject)) {
					var content = EditorGUIUtility.ObjectContent(hitGroup.Key, typeof(GameObject));
					EditorGUI.indentLevel = 0;
					if (GUILayout.Button(content, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(Screen.width / 2.0f - 6.0f))) {
						EditorGUIUtility.PingObject(hitGroup.Key);
						Selection.activeGameObject = hitGroup.Key;
					}

					foreach (var hitInfo in hitGroup) {


						EditorGUI.indentLevel = 1;
						EditorGUILayout.PropertyField(hitInfo.property, new GUIContent(hitInfo.property.propertyPath), true);

					}

					//EditorGUILayout.TextArea(hitInfo.hitProperty, GUILayout.Width(Screen.width / 2.0f - 6.0f));
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
		hitInfoList.Clear();

		Object[] targetObjects = null;
		if (!!includeSubObject) {
			targetObjects = EditorUtility.CollectDeepHierarchy(new Object[] { referencedObject });
		} else {
			targetObjects = new Object[] { referencedObject };
		}

		referenceMap = EditorExtentionUtility.buildReferenceMap(targetObjects);
		foreach (var item in referenceMap) {
			foreach (var item2 in item.Value) {
				SearchHitInfo info = new SearchHitInfo();

				switch (item2.serializedObject.targetObject) {
					case Component component:
						info.hitGameObject = component.gameObject;
						break;
					case GameObject gameObject:
						info.hitGameObject = gameObject;
						break;
					default:
						break;
				}

				info.property = item2;
				hitInfoList.Add(info);
				//info.hitGameObject =
				Debug.Log(item2.propertyPath);
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

	/// <summary>
	/// includeInvisibleProperties を見て、イテレータを進める
	/// </summary>
	/// <param name="iterator"></param>
	/// <returns></returns>
	bool conditionalIteratorNext(SerializedProperty iterator)
	{
		return includeInvisibleProperties ? iterator.Next(true) : iterator.NextVisible(true);
	}
}
