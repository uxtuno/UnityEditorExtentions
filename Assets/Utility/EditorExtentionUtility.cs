using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class EditorExtentionUtility
{
	/// <summary>
	/// 引数に指定した、Object群を参照する、プロパティを特定するDictionaryを構築する
	/// </summary>
	/// <param name="referencedObjects"></param>
	public static Dictionary<Object, List<SerializedProperty>> buildReferenceMap(Object[] referencedObjects)
	{
		var allComponents = Resources.FindObjectsOfTypeAll<Component>();
		var referenceMap = new Dictionary<Object, List<SerializedProperty>>();

		foreach (var component in allComponents) {
			// Scene上のオブジェクトのみ対象にする
			if (!component.gameObject.scene.isLoaded) {
				continue;
			}

			var serializedObject = new SerializedObject(component);
			var iterator = serializedObject.GetIterator();
			while (iterator.Next(true)) {
				if (iterator.propertyType != SerializedPropertyType.ObjectReference || isSealedProperty(iterator.propertyPath)) {
					continue;
				}

				foreach (var referencedObject in referencedObjects) {
					// referencedObjectを参照しているプロパティをリストアップ
					if (iterator.objectReferenceValue == referencedObject) {
						if (!referenceMap.ContainsKey(referencedObject)) {
							referenceMap.Add(referencedObject, new List<SerializedProperty>());
						}
						referenceMap[referencedObject].Add(iterator.Copy());
					}
				}
			}
		}
		return referenceMap;
	}

	/// <summary>
	/// 特定のプロパティは、変更するとオブジェクトの構造を破壊してしまう可能性が高い、
	/// そのようなプロパティを判定するためのメソッド
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static bool isSealedProperty(string path)
	{
		return path == "m_GameObject" ||
			path == "m_FileID" ||
			path.Contains("m_CorrespondingSourceObject") ||
			path.Contains("m_PrefabInstance") ||
			path.Contains("m_PrefabAsset") ||
			path.Contains("m_Father") ||
			path.Contains("m_Children");
	}

	/// <summary>
	/// Object構造を、パスでマッピング
	/// </summary>
	/// <param name="rootGameObject"></param>
	/// <returns></returns>
	public static void buildObjectPathMap(Object rootGameObject, string context, Dictionary<string, Object> outObjectPathTree)
	{
		var contextPath = context;
		outObjectPathTree.Add(context, rootGameObject);

		switch (rootGameObject) {
		case GameObject gameObject:
			var index = 0;
			// コンポーネント列挙
			foreach (var component in gameObject.GetComponents<Component>()) {
				buildObjectPathMap(component, context + "/" + $"component[{index}]", outObjectPathTree);
				++index;
			}

			index = 0;
			// 子オブジェクト列挙
			foreach (Transform child in gameObject.transform) {
				buildObjectPathMap(child.gameObject, context + "/" + $"gameObject[{index}]", outObjectPathTree);
				++index;
			}

			break;
		default:
			break;
		}
	}

}
