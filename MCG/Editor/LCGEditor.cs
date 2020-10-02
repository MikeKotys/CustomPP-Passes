using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace MCGPostEffect
{
	[CustomEditor(typeof(LCG))]
	[CanEditMultipleObjects]
	public sealed class LCGEditor : Editor
	{

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			LCG lcg = target as LCG;

			if (lcg != null)
			{
				//Texture2D newTexture2D;
				//newTexture2D = EditorGUILayout.ObjectField(lcg.LUT2D, typeof(Texture2D), false) as Texture2D;

				//if (newTexture2D != null && lcg.LUT2D != newTexture2D)
				//{
				//	if (!LCG.ValidDimensions(newTexture2D))
				//		EditorUtility.DisplayDialog("Error!", 
				//			"Invalid texture dimensions!\nPick another texture or adjust dimension to e.g. 256x16.",
				//			"Aw snap!");
				//	else
				//	{
				//		string path = AssetDatabase.GetAssetPath(newTexture2D);
				//		TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
				//		bool doImport = textureImporter.isReadable == true;

				//		if (doImport)
				//		{
				//			textureImporter.isReadable = true;
				//			textureImporter.mipmapEnabled = false;
				//			textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
				//			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
				//		}

				//		//if (newTexture2D == null)
				//		//	EditorUtility.DisplayDialog("Error!", "LCG Texture has not been assigned", 
				//		//		"I am so ashamed of my actions", "My revenge would shake the Earth!");
				//		//else
				//			lcg.AssignTextureAndConvert(newTexture2D);
				//	}
				//}

				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Previous LUT", GUILayout.Width(115), GUILayout.Height(20)))
					SetNextLUT(lcg, next: false);
				if (GUILayout.Button("Next LUT", GUILayout.Width(115), GUILayout.Height(20)))
					SetNextLUT(lcg, next: true);
				GUILayout.EndHorizontal();
			}
		}

		[MenuItem("GameObject/LCG - Set previous LUT _;")]
		static void SetPrevious()
		{
			if (Selection.activeGameObject != null && Selection.activeGameObject.activeSelf)
			{
				var lcg = Selection.activeGameObject.GetComponent<LCG>();
				if (lcg != null)
					SetNextLUT(lcg, false);
			}
		}

		[MenuItem("GameObject/LCG - Set next LUT _'")]
		static void SetNext()
		{
			if (Selection.activeGameObject != null && Selection.activeGameObject.activeSelf)
			{
				var lcg = Selection.activeGameObject.GetComponent<LCG>();
				if (lcg != null)
					SetNextLUT(lcg, true);
			}
		}

		[MenuItem("GameObject/LCG - Turn Off LCG _o")]
		static void TurnOffLCG()
		{
			if (Selection.activeGameObject != null && Selection.activeGameObject.activeSelf)
			{
				var lcg = Selection.activeGameObject.GetComponent<LCG>();
				if (lcg != null)
					lcg.enabled = !lcg.enabled;
			}
		}

		static void SetNextLUT(LCG lcg, bool next)
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
