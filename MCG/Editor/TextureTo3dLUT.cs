using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

namespace MCGPostEffect
{
	[ExecuteInEditMode]
	public static class TextureTo3dLUT
	{
		//public static void SetIdentityLut()
		//{
		//	int dim = 16;
		//	var newC = new Color[dim * dim * dim];
		//	float oneOverDim = 1.0f / (1.0f * dim - 1.0f);

		//	for (int i = 0; i < dim; i++)
		//		for (int j = 0; j < dim; j++)
		//			for (int k = 0; k < dim; k++)
		//				newC[i + (j * dim) + (k * dim * dim)]
		//					= new Color((i * 1.0f) * oneOverDim, (j * 1.0f) * oneOverDim, (k * 1.0f) * oneOverDim, 1.0f);

		//	var texture3D = new Texture3D(dim, dim, dim, TextureFormat.ARGB32, false);
		//	texture3D.SetPixels(newC);
		//	texture3D.Apply();
		//	texture3D.filterMode = FilterMode.Trilinear;
		//	texture3D.name = "_Identity_";

		//	var otherSerializedObj = new SerializedObject(this);
		//	otherSerializedObj.FindProperty("Texture3DSerialized").objectReferenceValue = texture3D;
		//	otherSerializedObj.ApplyModifiedProperties();
		//}

		public static bool ValidDimensions(Texture2D tex2d)
		{
			if (!tex2d) return false;
			int h = tex2d.height;

			if (h != Mathf.FloorToInt(Mathf.Sqrt(tex2d.width)))
				return false;
			else
				return true;
		}

		/// <summary>Converts DisplayLUTTexture2D into a Texture3D and stores it in the Texture3Ds List.</summary>
		[MenuItem("GameObject/LCG - Convert All LUTs in this Dir")]

		public static void AssignTextureAndConvert()
		{
			var newTexture2D = Selection.activeObject as Texture2D;
			if (newTexture2D != null)
			{
				string t2DPath = AssetDatabase.GetAssetPath(newTexture2D);
				var allLUTs = AssetDatabase.FindAssets("t:texture2D",
					new string[1] { Path.GetDirectoryName(t2DPath) });

				int n = allLUTs.Length;
				while (--n > -1)
				{
					string path = AssetDatabase.GUIDToAssetPath(allLUTs[n]);
					newTexture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
					if (newTexture2D == null)
						EditorUtility.DisplayDialog("Error", "Could not load Texture2D!", "Aw snap!");
					else
					{
						// conversion fun: the given 2D texture needs to be of the format
						//  w * h, wheras h is the 'depth' (or 3d dimension 'dim') and w = dim * dim
						int dim = newTexture2D.width * newTexture2D.height;
						dim = newTexture2D.height;

						if (!ValidDimensions(newTexture2D))
							EditorUtility.DisplayDialog("Error",
								"The given 2D texture " + newTexture2D.name + " cannot be used as a 3D LUT.", "Aw snap!");
						else
						{
							var c = newTexture2D.GetPixels();
							var newC = new Color[c.Length];

							for (int i = 0; i < dim; i++)
								for (int j = 0; j < dim; j++)
									for (int k = 0; k < dim; k++)
									{
										int j_ = dim - j - 1;
										int num = i + (j * dim) + (k * dim * dim);
										newC[num] = c[k * dim + i + j_ * dim * dim];

										float oneOverDim = 1.0f / (1.0f * dim - 1.0f);

										newC[num] = new Color(newC[num].r, newC[num].g, newC[num].b, 1);
									}

							var texture3D = new Texture3D(dim, dim, dim, TextureFormat.ARGB32, false);
							texture3D.SetPixels(newC);
							texture3D.Apply();
							texture3D.filterMode = FilterMode.Trilinear;
							texture3D.name = newTexture2D.name;
							path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(newTexture2D));
							AssetDatabase.CreateAsset(texture3D, path + "/" + texture3D.name + ".cubemap");
						}
					}
				}
			}
		}
	}
}
