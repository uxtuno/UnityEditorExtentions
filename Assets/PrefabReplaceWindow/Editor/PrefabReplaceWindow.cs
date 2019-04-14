﻿namespace PrefabReplacer
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEditor;
	using System.Linq;

	/// <summary>
	/// Scene上のGameObjectを、指定したPrefabで置き換える エディタウインドウ
	/// </summary>
	public class PrefabReplaceWindow : EditorWindow
	{
		/// <summary>
		/// 置き換えるPrefab
		/// </summary>
		GameObject replacePrefab;

		SerializedProperty replaceRootGameObjectsProperty;

		Vector2 scrollPosition;

		[MenuItem("Window/Prefab Replacer")]
		static void ShowWindow()
		{
			GetWindow<PrefabReplaceWindow>();
		}

		void OnEnable()
		{
			var serializedObject = new SerializedObject(this);
			replaceRootGameObjectsProperty = serializedObject.FindProperty("replaceRootGameObjects");

			Selection.selectionChanged += () => {
				Repaint();
			};
		}

		void OnGUI()
		{
			replacePrefab = EditorGUILayout.ObjectField("Replace Prefab", replacePrefab, typeof(GameObject), true) as GameObject;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Replace GameObject Root Objects");

			using (var scrollScope = new EditorGUILayout.ScrollViewScope(scrollPosition)) {
				foreach (var target in Selection.gameObjects) {
					using (new EditorGUI.DisabledGroupScope(true)) {
						EditorGUILayout.ObjectField(target, typeof(GameObject), true);
					}
				}

				scrollPosition = scrollScope.scrollPosition;
			}

			if (GUILayout.Button("Connect")) {
				// Applyが必要な、SerializedObject
				var applySerializedObjects = new HashSet<SerializedObject>();

				foreach (var replaceObject in Selection.gameObjects) {
					var instancedPrefab = PrefabUtility.InstantiatePrefab(replacePrefab) as GameObject;
					instancedPrefab.transform.SetParent(replaceObject.transform.parent);
					instancedPrefab.transform.SetSiblingIndex(replaceObject.transform.GetSiblingIndex());

					var outPrefabInstanceProperties = new Dictionary<string, SerializedProperty>();
					var outSkipObjects = new HashSet<Object>();
					GatheringProperties(instancedPrefab, instancedPrefab, "", outPrefabInstanceProperties, outSkipObjects);

					var outReplaceObjectProperties = new Dictionary<string, SerializedProperty>();
					var outProcessedObjtects = new HashSet<Object>();
					GatheringProperties(replaceObject, replaceObject, "", outReplaceObjectProperties, outProcessedObjtects);

					ReplaceReference(replaceObject, outSkipObjects, outReplaceObjectProperties, outPrefabInstanceProperties);

					var applyRequiredSerializedObjects = new HashSet<SerializedObject>();
					// Prefab インスタンスの各プロパティに対して、置き換え対象のGameObjectのプロパティを代入
					foreach (var propertyInfo in outReplaceObjectProperties) {
						if (outPrefabInstanceProperties.ContainsKey(propertyInfo.Key)) {
							var replaceProperty = outPrefabInstanceProperties[propertyInfo.Key];

							switch (replaceProperty.propertyType) {
							case SerializedPropertyType.Integer:
								replaceProperty.intValue = propertyInfo.Value.intValue;
								break;
							case SerializedPropertyType.Boolean:
								replaceProperty.boolValue = propertyInfo.Value.boolValue;
								break;
							case SerializedPropertyType.Float:
								replaceProperty.floatValue = propertyInfo.Value.floatValue;
								break;
							case SerializedPropertyType.String:
								replaceProperty.stringValue = propertyInfo.Value.stringValue;
								break;
							case SerializedPropertyType.Color:
								replaceProperty.colorValue = propertyInfo.Value.colorValue;
								break;
							case SerializedPropertyType.LayerMask:
								replaceProperty.intValue = propertyInfo.Value.intValue;
								break;
							case SerializedPropertyType.Enum:
								replaceProperty.enumValueIndex = propertyInfo.Value.enumValueIndex;
								break;
							case SerializedPropertyType.Vector2:
								replaceProperty.vector2Value = propertyInfo.Value.vector2Value;
								break;
							case SerializedPropertyType.Vector3:
								replaceProperty.vector3Value = propertyInfo.Value.vector3Value;
								break;
							case SerializedPropertyType.Vector4:
								replaceProperty.vector4Value = propertyInfo.Value.vector4Value;
								break;
							case SerializedPropertyType.Rect:
								replaceProperty.rectValue = propertyInfo.Value.rectValue;
								break;
							case SerializedPropertyType.AnimationCurve:
								replaceProperty.animationCurveValue = propertyInfo.Value.animationCurveValue;
								break;
							case SerializedPropertyType.Bounds:
								replaceProperty.boundsValue = propertyInfo.Value.boundsValue;
								break;
							case SerializedPropertyType.Gradient:
								break;
							case SerializedPropertyType.Quaternion:
								replaceProperty.quaternionValue = propertyInfo.Value.quaternionValue;
								break;
							case SerializedPropertyType.ObjectReference:
								if (!outProcessedObjtects.Contains(propertyInfo.Value.objectReferenceValue)) {
									replaceProperty.objectReferenceValue = propertyInfo.Value.objectReferenceValue;
								}
								break;
							case SerializedPropertyType.ExposedReference:
								replaceProperty.exposedReferenceValue = propertyInfo.Value.exposedReferenceValue;
								break;
							case SerializedPropertyType.Vector2Int:
								replaceProperty.vector2IntValue = propertyInfo.Value.vector2IntValue;
								break;
							case SerializedPropertyType.Vector3Int:
								replaceProperty.vector3IntValue = propertyInfo.Value.vector3IntValue;
								break;
							case SerializedPropertyType.RectInt:
								replaceProperty.rectIntValue = propertyInfo.Value.rectIntValue;
								break;
							case SerializedPropertyType.BoundsInt:
								replaceProperty.boundsIntValue = propertyInfo.Value.boundsIntValue;
								break;
							default:
								break;
							}

							if (!applySerializedObjects.Contains(replaceProperty.serializedObject)) {
								applySerializedObjects.Add(replaceProperty.serializedObject);
							}
						}
					}

					DestroyImmediate(replaceObject);
				}
				// Apply が必要な、SerializedObjectを最後に一気にApplyする
				foreach (var item in applySerializedObjects) {
					item.ApplyModifiedProperties();
				}
			}
		}

		/// <summary>
		/// target の要素を参照しているすべてのプロパティを、
		/// </summary>
		/// <param name="target"></param>
		void ReplaceReference(GameObject target, HashSet<Object> skipObjects, Dictionary<string, SerializedProperty> targetProperties, Dictionary<string, SerializedProperty> replaceProperties)
		{
			var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

			// serializedObject.targetObject の重複を省いたリストを作成
			var distincetTargetProperties = targetProperties.GroupBy(property => property.Value.serializedObject.targetObject).Select(property => property.First());

			foreach (var gameObject in gameObjects) {
				if (gameObject == target) {
					continue;
				}
				
				foreach (var component in gameObject.GetComponents<Component>()) {
					var serializedObject = new SerializedObject(component);
					var iterator = serializedObject.GetIterator();
					while (iterator.Next(true)) {
						if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
							foreach (var item in distincetTargetProperties) {
								if (iterator.objectReferenceValue == item.Value.serializedObject.targetObject) {
									if (iterator.propertyPath.Contains("m_GameObject") ||
										iterator.propertyPath.Contains("m_Children.Array.data") ||
										iterator.propertyPath.Contains("m_ComponentOwner") ||
										iterator.propertyPath.Contains("m_Father")
										) {
										continue;
									}
									iterator.objectReferenceValue = replaceProperties[item.Key].serializedObject.targetObject;
								}
							}
						}
					}
					serializedObject.ApplyModifiedProperties();
				}
			}
		}

		/// <summary>
		/// シリアライズされているプロパティを収集する
		/// </summary>
		/// <param name="target"></param>
		/// <param name="context"></param>
		/// <param name="outProperties"></param>
		void GatheringProperties(GameObject root, Object target, string context, Dictionary<string, SerializedProperty> outProperties, HashSet<Object> processedObjects = null)
		{
			if (processedObjects == null) {
				processedObjects = new HashSet<Object>();
			}

			// 循環参照を避けるための判定
			if (processedObjects.Contains(target)) {
				return;
			}
			// 再帰するので、初めに判定して追加しておく必要がある
			processedObjects.Add(target);

			var serializedObject = new SerializedObject(target);
			var iterator = serializedObject.GetIterator();
			while (iterator.Next(true)) {
				if (iterator.name == "m_FileID" ||
					iterator.propertyPath.Contains("m_CorrespondingSourceObject") ||
					iterator.propertyPath.Contains("m_PrefabInstance") ||
					iterator.propertyPath.Contains("m_PrefabAsset")
					) {
					// オブジェクトの構造を破壊するタイプのプロパティはスキップ
					continue;
				}

				var contextPropertyPath = GetContextualPropertyPath(context, iterator);
				if (outProperties.ContainsKey(contextPropertyPath)) {
					continue; // すでに処理済み
				} else {
					outProperties.Add(contextPropertyPath, iterator.Copy());
				}

				if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
					ConditionalGatheringProperties(root, iterator, context, outProperties, processedObjects);
				}
			}
		}

		/// <summary>
		/// SerializedProperty の型によって、継続するか判断する
		/// </summary>
		/// <param name="property"></param>
		/// <param name="context"></param>
		/// <param name="outProperties"></param>
		void ConditionalGatheringProperties(GameObject root, SerializedProperty property, string context, Dictionary<string, SerializedProperty> outProperties, HashSet<Object> processedObjects)
		{
			var serializedObjectReference = property.objectReferenceValue;
			if ((serializedObjectReference is GameObject) || (serializedObjectReference is Component)) {
				if (IsChildOf(root, serializedObjectReference)) {
					GatheringProperties(root, serializedObjectReference, GetContextualPropertyPath(context, property), outProperties, processedObjects);
				}
			}
		}

		/// <summary>
		/// SerializedProperty を一意に識別するためのパスを返す
		/// </summary>
		/// <param name="context"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		string GetContextualPropertyPath(string context, SerializedProperty property)
		{
			return context + "/" + property.propertyPath;
		}

		/// <summary>
		/// 指定した、Objectが、Rootの子階層に保持しているプロパティかどうかを判定する
		/// </summary>
		/// <param name="root"></param>
		/// <param name="property"></param>
		/// <returns></returns>
		bool IsChildOf(GameObject root, Object property)
		{
			if (root == property) {
				return true;
			}

			switch (property) {
			case GameObject gameObject:
				if (!gameObject.transform.parent) {
					return false;
				}
				return IsChildOf(root, gameObject.transform.parent.gameObject);

			case Component component:
				return IsChildOf(root, component.gameObject);
			default:
				break;
			}
			return false;
		}
	}
}
