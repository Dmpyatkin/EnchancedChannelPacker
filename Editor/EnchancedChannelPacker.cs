#if UNITY_EDITOR
/*
Original code of MaskPacke from: https://www.reddit.com/r/Unity3D/comments/glkvp2/i_made_another_mask_map_packer_for_hdrp/
ChannelPacker version is a heavily modified / rewritten version from Camobiwon: https://github.com/camobiwon/ChannelPacker
I've also made some changes to this ChannelPacker code, making it more handy for my usecases (Enchanced by dmpyatkin: )
Thank you original creator and heavily-modifier! This has been extremely useful to me too, and whoever is using this, I hope Enchanced Channel Packer is useful to you as well
Have a nice day!
*/

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;

namespace EnchancedChannelPacker
{
	public class EnchancedChannelPacker : EditorWindow
	{
		//Use a compute shader to greatly speed up packing time
		[SerializeField]
		private ComputeShader fastPack;

		private static EnchancedChannelPacker window;
		
		[SerializeField]
		public ChannelPackerPreset preset;
		[SerializeField]
		public ChannelPackerSettings settings;

		//Inputs
		[SerializeField]
		private Texture2D[] inputs = new Texture2D[4];
		[SerializeField]
		public float[] defaults = new float[4];
		[SerializeField]
		public float[] mults = new float[4] { 1, 1, 1, 1 };
		[SerializeField]
		public ColorChannel[] froms = new ColorChannel[4];
		[SerializeField]
		public bool[] inverts = new bool[4];

		private RenderTexture[] blits = new RenderTexture[4];

		private Vector2 scrollPos;
		private GUIStyle regularStyle, regularSmall, smallWarn, regularWarn;
		private RenderTexture packedTexture;
		private Texture2D finalTexture;
		private Vector2Int textureDimensions;
		private Editor previewEditor;

		// Flag to debounce packing
		private bool needsRepack = false;

		//Show the window
		[MenuItem("Tools/Enchanced Channel Packer")]
		public static void ShowWindow()
		{
			window = (EnchancedChannelPacker)GetWindow(typeof(EnchancedChannelPacker), false, "Enchanced Channel Packer");
		}

		private void OnEnable()
		{
			LoadSettings();
			InitGUIStyles();
			textureDimensions = Vector2Int.zero;
		}

		//If for some reason the window becomes null, get it again.
		private void OnInspectorUpdate()
		{
			if (!window)
				window = (EnchancedChannelPacker)GetWindow(typeof(EnchancedChannelPacker), false, "Enchanced Channel Packer");
		}

		private void OnGUI()
		{
			if (window)
			{
				window.Repaint();
				GUILayout.BeginArea(new Rect(0, 0, window.position.size.x, window.position.size.y));
				GUILayout.BeginVertical();
				scrollPos = GUILayout.BeginScrollView(scrollPos, false, true, GUILayout.ExpandHeight(true));
			}

			if (!inputs[0] && !inputs[1] && !inputs[2] && !inputs[3])
				textureDimensions = Vector2Int.zero;

			GUILayout.Label("Enchanced Channel Packer", regularStyle);
			GUILayout.Label("Add textures to be packed together", regularStyle);

			//Inputs
			EditorGUI.BeginChangeCheck();
			ChannelInput(0); //Red
			ChannelInput(1); //Green
			ChannelInput(2); //Blue
			ChannelInput(3); //Alpha
			bool inputsChanged = EditorGUI.EndChangeCheck();

			// Ensure textureDimensions is reset if all inputs are now null (handles removal in same frame)
			if (!inputs[0] && !inputs[1] && !inputs[2] && !inputs[3])
				textureDimensions = Vector2Int.zero;

			// Mark for repack if inputs changed
			if (inputsChanged && textureDimensions != Vector2Int.zero)
			{
				needsRepack = true;
			}

			//Input field for each color channel
			void ChannelInput(int channelInput)
			{
				GUILayout.BeginVertical(EditorStyles.helpBox);
				
				EditorGUI.BeginChangeCheck();
				Texture2D newInput = (Texture2D)EditorGUILayout.ObjectField($"{preset.names[channelInput]} Input", inputs[channelInput], typeof(Texture2D), false);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(this, "Change Input Texture");
					inputs[channelInput] = newInput;
				}
				
				if (!inputs[channelInput])
				{
					GUILayout.Label($"No {preset.names[channelInput]} Input, use slider to set value", regularSmall);
					EditorGUI.BeginChangeCheck();
					float newDefault = EditorGUILayout.Slider(defaults[channelInput], 0f, 1f);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(this, "Change Default Value");
						defaults[channelInput] = newDefault;
					}
				}
				else
				{
					if (textureDimensions != Vector2Int.zero && (inputs[channelInput].width != textureDimensions.x || inputs[channelInput].height != textureDimensions.y))
					{
						inputs[channelInput] = null;
						Debug.LogWarning("Input texture is not the same resolution as other textures! Rejecting");
					}
					if (textureDimensions == Vector2Int.zero)
					{
						textureDimensions.x = inputs[channelInput].width;
						textureDimensions.y = inputs[channelInput].height;
					}

					EditorGUI.BeginChangeCheck();
					ColorChannel newFrom = (ColorChannel)EditorGUILayout.EnumPopup("From Channel", froms[channelInput]);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(this, "Change Source Channel");
						froms[channelInput] = newFrom;
					}
					
					EditorGUI.BeginChangeCheck();
					float newMult = EditorGUILayout.Slider($"Multiplier", mults[channelInput], 0f, 1f);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(this, "Change Multiplier");
						mults[channelInput] = newMult;
					}
					
					EditorGUI.BeginChangeCheck();
					bool newInvert = EditorGUILayout.Toggle("Invert", inverts[channelInput]);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(this, "Change Invert");
						inverts[channelInput] = newInvert;
					}
					
					if (inputs[channelInput] && inputs[channelInput].graphicsFormat.ToString().Contains("SRGB"))
						GUILayout.Label("Texture marked as sRGB! Disabling recommended", smallWarn);
				}
				GUILayout.EndVertical();
			}

			GUILayout.Space(5f);

			//Main Options
			GUILayout.BeginVertical(EditorStyles.helpBox);
			if (GUILayout.Button("Pack Texture") && textureDimensions != Vector2Int.zero)
			{
				needsRepack = true;  // Force repack
				SaveTexture();
				EditorUtility.ClearProgressBar();
			}
			if (GUILayout.Button("Clear All"))
			{
				Undo.RecordObject(this, "Clear All");
				
				// Clear textures
				inputs[0] = inputs[1] = inputs[2] = inputs[3] = null;
				textureDimensions = Vector2Int.zero;
				
				// Reset all settings to defaults
				for (int i = 0; i < 4; i++)
				{
					mults[i] = 1f;
					inverts[i] = false;
					froms[i] = ColorChannel.R;
					defaults[i] = 0f;
				}
				
				// Clean up preview
				if (previewEditor != null)
				{
					DestroyImmediate(previewEditor);
					previewEditor = null;
				}
				if (finalTexture != null)
				{
					DestroyImmediate(finalTexture);
					finalTexture = null;
				}
				needsRepack = false;
			}
			if (GUILayout.Button("Save Preset"))
			{
				SavePreset();
			}

			EditorGUI.BeginChangeCheck();
			ChannelPackerPreset newPreset = (ChannelPackerPreset)EditorGUILayout.ObjectField(new GUIContent("Preset", "The preset packing settings to be used"), preset, typeof(ChannelPackerPreset), preset);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(this, "Change Preset");
				preset = newPreset;
				LoadSettings();
				if (textureDimensions != Vector2Int.zero)
				{
					needsRepack = true;
				}
			}

			if (GUILayout.Button("Reload Preset"))
			{
				Undo.RecordObject(this, "Reload Preset");
				LoadSettings();
				if (textureDimensions != Vector2Int.zero)
				{
					needsRepack = true;
				}
			}

			GUILayout.EndVertical();
			GUILayout.Space(5f);
			GUILayout.BeginVertical(EditorStyles.helpBox);

			//Preview
			if (GUILayout.Button("Update Preview") && textureDimensions != Vector2Int.zero)
			{
				needsRepack = true;
			}

			// Perform repack only if needed and not already in progress
			if (needsRepack && textureDimensions != Vector2Int.zero)
			{
				EditorUtility.DisplayProgressBar("Packing texture", "", 0f);
				CreatePackedTexture();
				needsRepack = false;  // Reset flag after packing
				EditorUtility.ClearProgressBar();
			}

			if (previewEditor != null)
			{
				GUILayout.Label("Preview", regularStyle);

				// Add the settings header (channel toggles and mip slider)
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (previewEditor.HasPreviewGUI())
				{
					previewEditor.OnPreviewSettings();
				}
				GUILayout.EndHorizontal();

				// Render the texture preview
				previewEditor.OnPreviewGUI(GUILayoutUtility.GetRect(256, 256), EditorStyles.objectField);
				GUILayout.Space(10f);
			}
			else
			{
				GUILayout.Label("Generate a preview to see the packed texture.", regularWarn);
			}

			GUILayout.EndVertical();

			if (window)
			{
				GUILayout.EndScrollView();
				GUILayout.EndVertical();
				GUILayout.EndArea();
			}
		}

		private void CreatePackedTexture()
		{
			if (textureDimensions == Vector2Int.zero)
			{
				return;
			}

			// Reuse finalTexture if dimensions match; otherwise, recreate
			if (finalTexture == null || finalTexture.width != textureDimensions.x || finalTexture.height != textureDimensions.y)
			{
				if (finalTexture != null)
				{
					DestroyImmediate(finalTexture);
				}
				finalTexture = new Texture2D(textureDimensions.x, textureDimensions.y, TextureFormat.ARGB32, false, true);
			}

			int blitKernel = fastPack.FindKernel("ChannelSet");

			PackTexture(0); //Red
			PackTexture(1); //Green
			PackTexture(2); //Blue
			PackTexture(3); //Alpha

			void PackTexture(int channelInput)
			{
				EditorUtility.DisplayProgressBar($"Packing {preset.names[channelInput]}", "", 1f);
				blits[channelInput] = new RenderTexture(textureDimensions.x, textureDimensions.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
				if (inputs[channelInput])
					Graphics.Blit(inputs[channelInput], blits[channelInput]);
				else
				{
					blits[channelInput].enableRandomWrite = true;
					blits[channelInput].Create();

					fastPack.SetTexture(blitKernel, "Packed", blits[channelInput]);
					fastPack.SetFloat("packedCol", defaults[channelInput]);
					fastPack.Dispatch(blitKernel, textureDimensions.x, textureDimensions.y, 1);
				}
			}

			EditorUtility.DisplayProgressBar("Combining Maps", "", 1f);
			packedTexture = new RenderTexture(textureDimensions.x, textureDimensions.y, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			packedTexture.enableRandomWrite = true;
			packedTexture.Create();

			int kernel = fastPack.FindKernel("CSMain");
			fastPack.SetTexture(kernel, "Result", packedTexture);
			fastPack.SetTexture(kernel, "r", blits[0]);
			fastPack.SetTexture(kernel, "g", blits[1]);
			fastPack.SetTexture(kernel, "b", blits[2]);
			fastPack.SetTexture(kernel, "a", blits[3]);

			fastPack.SetInts("froms", inputs[0] ? (int)froms[0] : 0, inputs[1] ? (int)froms[1] : 0, inputs[2] ? (int)froms[2] : 0, inputs[3] ? (int)froms[3] : 0);
			fastPack.SetInts("inverts", inputs[0] ? (inverts[0] ? 1 : 0) : 0, inputs[1] ? (inverts[1] ? 1 : 0) : 0, inputs[2] ? (inverts[2] ? 1 : 0) : 0, inputs[3] ? (inverts[3] ? 1 : 0) : 0);
			fastPack.SetFloats("mults", inputs[0] ? mults[0] : 1, inputs[1] ? mults[1] : 1, inputs[2] ? mults[2] : 1, inputs[3] ? mults[3] : 1);
			fastPack.Dispatch(kernel, textureDimensions.x, textureDimensions.y, 1);

			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = packedTexture;
			finalTexture.ReadPixels(new Rect(0, 0, packedTexture.width, packedTexture.height), 0, 0);
			finalTexture.Apply();
			RenderTexture.active = previous;

			// Recreate previewEditor only if needed (e.g., first time or after clear)
			if (previewEditor == null)
			{
				previewEditor = Editor.CreateEditor(finalTexture);
			}
		}

		private void SaveTexture()
		{
			//Find non-null channel input, bleh
			Texture2D validTex;
			if (inputs[0] != null)
				validTex = inputs[0];
			else if (inputs[1] != null)
				validTex = inputs[1];
			else if (inputs[2] != null)
				validTex = inputs[2];
			else
				validTex = inputs[3];

			string texPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(validTex));

			string path = EditorUtility.SaveFilePanelInProject("Save Texture To Directory", "PackedTexture", "png", "Saved", texPath);
			byte[] pngData = finalTexture.EncodeToPNG();

			//Export to directory
			if (path.Length != 0 && pngData != null)
			{
				File.WriteAllBytes(path, pngData);
				Debug.Log($"Packed texture saved to: {path}");
				AssetDatabase.Refresh();

				//Disable sRGB
				TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
				importer.sRGBTexture = false;
				importer.SaveAndReimport();
			}
			else
				EditorUtility.ClearProgressBar();
		}

		private void LoadSettings()
		{
			try
			{ //Get settings
				settings = (ChannelPackerSettings)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ChannelPackerSettings")[0]), typeof(ChannelPackerSettings)); //Get settings
			}
			catch
			{ //If no settings file, create and assign
				ChannelPackerSettings created = ScriptableObject.CreateInstance<ChannelPackerSettings>();
				string path = "Assets/Plugins/ChannelPacker/ChannelPackerSettings.asset";
				AssetDatabase.CreateAsset(created, path);
				settings = AssetDatabase.LoadAssetAtPath<ChannelPackerSettings>(path); //CreateAsset doesn't return anything :(
			}

			if (preset == null)
			{
				if (settings.lastPreset == null)
				{
					try
					{ //Get preset
						preset = (ChannelPackerPreset)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ChannelPackerPreset")[0]), typeof(ChannelPackerPreset)); //Get settings
					}
					catch
					{ //If no settings file, create and assign
						ChannelPackerPreset created = ScriptableObject.CreateInstance<ChannelPackerPreset>();
						string path = "Assets/Plugins/ChannelPacker/ChannelPackerDefault.asset";
						AssetDatabase.CreateAsset(created, path);
						preset = AssetDatabase.LoadAssetAtPath<ChannelPackerPreset>(path); //CreateAsset doesn't return anything :(
					}
				}
				else
				{
					preset = settings.lastPreset;
				}
			}

			settings.lastPreset = preset;

			EditorUtility.SetDirty(settings);

			//Pull settings from preset
			Array.Copy(preset.defaults, defaults, 4);
			Array.Copy(preset.froms, froms, 4);
			Array.Copy(preset.inverts, inverts, 4);
		}

		private void SavePreset()
		{
			//Create new preset SO
			string presetPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(preset));
			string path = EditorUtility.SaveFilePanelInProject("Save Preset To Directory", "Preset", "asset", "Saved", presetPath);

			if (path.Length != 0)
			{
				ChannelPackerPreset created = ScriptableObject.Instantiate<ChannelPackerPreset>(preset); //Copy current preset

				//Copy editable values from window
				Array.Copy(defaults, created.defaults, 4);
				Array.Copy(froms, created.froms, 4);
				Array.Copy(inverts, created.inverts, 4);

				AssetDatabase.CreateAsset(created, path);
				preset = AssetDatabase.LoadAssetAtPath<ChannelPackerPreset>(path);

				EditorUtility.SetDirty(preset);

				Debug.Log($"Preset saved to: {path}");
				AssetDatabase.Refresh();
			}
		}

		private void InitGUIStyles()
		{
			regularStyle = new GUIStyle();
			regularStyle.fontSize = 14;
			regularStyle.fontStyle = FontStyle.Normal;
			regularStyle.wordWrap = true;
			regularStyle.alignment = TextAnchor.MiddleCenter;
			if (EditorGUIUtility.isProSkin)
				regularStyle.normal.textColor = new Color(0.76f, 0.76f, 0.76f, 1f);
			else
				regularStyle.normal.textColor = Color.black;

			regularSmall = new GUIStyle();
			regularSmall.fontSize = 12;
			regularSmall.fontStyle = FontStyle.Normal;
			regularSmall.wordWrap = true;
			regularSmall.alignment = TextAnchor.MiddleCenter;
			if (EditorGUIUtility.isProSkin)
				regularSmall.normal.textColor = new Color(0.76f, 0.76f, 0.76f, 1f);
			else
				regularSmall.normal.textColor = Color.black;

			smallWarn = new GUIStyle();
			smallWarn.fontSize = 12;
			smallWarn.fontStyle = FontStyle.Normal;
			smallWarn.wordWrap = true;
			smallWarn.alignment = TextAnchor.MiddleCenter;
			if (EditorGUIUtility.isProSkin)
				smallWarn.normal.textColor = new Color(0.90f, 0.65f, 0.10f, 1f);
			else
				smallWarn.normal.textColor = new Color(0.60f, 0.35f, 0.00f, 1f);

			regularWarn = new GUIStyle();
			regularWarn.fontSize = 14;
			regularWarn.fontStyle = FontStyle.Normal;
			regularWarn.wordWrap = true;
			regularWarn.alignment = TextAnchor.MiddleCenter;
			if (EditorGUIUtility.isProSkin)
				regularWarn.normal.textColor = new Color(0.90f, 0.65f, 0.10f, 1f);
			else
				regularWarn.normal.textColor = new Color(0.60f, 0.35f, 0.00f, 1f);
		}

		private void OnDisable()
		{
			if (previewEditor != null)
			{
				DestroyImmediate(previewEditor);
			}
			if (finalTexture != null)
			{
				DestroyImmediate(finalTexture);
			}
		}

		public enum ColorChannel
		{
			R, G, B, A
		}
	}
}
#endif