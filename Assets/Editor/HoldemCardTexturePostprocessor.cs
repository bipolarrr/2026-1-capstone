using System;
using UnityEditor;
using UnityEngine;

public sealed class HoldemCardTexturePostprocessor : AssetPostprocessor
{
	const string CardSpriteRoot = "Assets/Holdem/Sprites/Cards/";

	void OnPreprocessTexture()
	{
		string normalizedPath = assetPath.Replace('\\', '/');
		if (!normalizedPath.StartsWith(CardSpriteRoot, StringComparison.Ordinal))
			return;

		var importer = (TextureImporter)assetImporter;
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Single;
		importer.filterMode = FilterMode.Point;
		importer.mipmapEnabled = false;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.alphaIsTransparency = true;
		importer.npotScale = TextureImporterNPOTScale.None;
		ConfigurePlatform(importer.GetDefaultPlatformTextureSettings(), importer.SetPlatformTextureSettings);
		ConfigurePlatform(importer.GetPlatformTextureSettings("Standalone"), importer.SetPlatformTextureSettings);

		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		settings.spriteMeshType = SpriteMeshType.FullRect;
		importer.SetTextureSettings(settings);
	}

	static void ConfigurePlatform(TextureImporterPlatformSettings settings,
		Action<TextureImporterPlatformSettings> apply)
	{
		settings.textureCompression = TextureImporterCompression.Uncompressed;
		apply(settings);
	}
}
