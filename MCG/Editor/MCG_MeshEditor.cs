using UnityEditor;
using UnityEngine;
using System.IO;

namespace MCGPostEffect
{
	[CustomEditor(typeof(MCG_Mesh))]
	[CanEditMultipleObjects]
	public sealed class MCG_MeshEditor : Editor
	{

		public override void OnInspectorGUI()
		{
			MCG_Mesh MCGmesh = target as MCG_Mesh;
			int oldPriority = MCGmesh.Priority;

			DrawDefaultInspector();

			if (MCGmesh.Priority != oldPriority)
			{
				MCGmesh.OnBecameInvisible();
				MCGmesh.OnBecameVisible();
			}

			if (MCGmesh != null)
			{
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Previous LUT", GUILayout.Width(115), GUILayout.Height(20)))
					SetNextLUT(MCGmesh, next: false);
				if (GUILayout.Button("Next LUT", GUILayout.Width(115), GUILayout.Height(20)))
					SetNextLUT(MCGmesh, next: true);
				GUILayout.EndHorizontal();
			}
		}

		[MenuItem("GameObject/LCG - Set previous LUT _;")]
		static void SetPrevious()
		{
			if (Selection.activeGameObject != null && Selection.activeGameObject.activeSelf)
			{
				var lcg = Selection.activeGameObject.GetComponent<MCG_Mesh>();
				if (lcg != null)
					SetNextLUT(lcg, false);
			}
		}

		[MenuItem("GameObject/LCG - Set next LUT _'")]
		static void SetNext()
		{
			if (Selection.activeGameObject != null && Selection.activeGameObject.activeSelf)
			{
				var lcg = Selection.activeGameObject.GetComponent<MCG_Mesh>();
				if (lcg != null)
					SetNextLUT(lcg, true);
			}
		}

		[MenuItem("GameObject/LCG - Turn Off LCG _o")]
		static void TurnOffLCG()
		{
			if (Selection.activeGameObject != null && Selection.activeGameObject.activeSelf)
			{
				var lcg = Selection.activeGameObject.GetComponent<MCG_Mesh>();
				if (lcg != null)
					lcg.enabled = !lcg.enabled;
			}
		}

		static void SetNextLUT(MCG_Mesh lcg, bool next)
		{
			if (lcg != null)
			{
				if (lcg.LUT3D == null)
					EditorUtility.DisplayDialog("Error!",
						"Please supply the first LUT (drop it from the menu onto the texture slot) before scrolling.",
						"Your word is an order for me!");
				else
				{
					string t3DPath = AssetDatabase.GetAssetPath(lcg.LUT3D);
					var allLUTs = AssetDatabase.FindAssets("t:texture3D",
						new string[1] { Path.GetDirectoryName(t3DPath) });
					int currentLUTNumber = -1;
					int n = allLUTs.Length;
					while (--n > -1)
					{
						if (AssetDatabase.GUIDToAssetPath(allLUTs[n]) == t3DPath)
						{
							currentLUTNumber = n;
							break;
						}
					}

					if (next)
					{
						n = currentLUTNumber + 1;
						if (n >= allLUTs.Length)
							n = 0;
					}
					else
					{
						n = currentLUTNumber - 1;
						if (n <= 0)
							n = allLUTs.Length - 1;
					}

					string path = AssetDatabase.GUIDToAssetPath(allLUTs[n]);
					Texture3D tex = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
					if (tex == null)
						EditorUtility.DisplayDialog("Error", "Could not load Texture3D!", "Aw snap!");
					else
					{
						var otherSerializedObj = new SerializedObject(lcg);
						otherSerializedObj.FindProperty("LUT3D").objectReferenceValue = tex;
						otherSerializedObj.ApplyModifiedProperties();
						lcg.UpdateLUT();
					}
				}
			}
		}
	}
}
