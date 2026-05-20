using System.IO;
using UnityEditor;
using UnityEngine;

public static class DicePrefabBuilder
{
	const string SourceTexturePath = "Assets/Dices/D6_mine.png";
	const string GeneratedFolder = "Assets/Dices/Generated";
	const string AtlasPath = GeneratedFolder + "/D6MineAtlas.png";
	const string MeshPath = GeneratedFolder + "/D6MineMesh.asset";
	const string MaterialPath = GeneratedFolder + "/D6Mine.mat";
	const string PrefabPath = "Assets/Dices/Prefabs/Dice_d6_mine.prefab";
	const string FaceSpriteFolder = "Assets/Textures/DiceFaces";

	const int Columns = 3;
	const int Rows = 2;
	const int OutputTileSize = 512;
	const float HalfSize = 0.68f;
	const float BackgroundValueThreshold = 0.45f;
	const float BackgroundSaturationThreshold = 0.12f;

	[MenuItem("Tools/Build Dice Prefabs/D6 Mine")]
	public static void BuildD6MinePrefab()
	{
		if (!File.Exists(SourceTexturePath))
		{
			Debug.LogError($"[DicePrefabBuilder] Source texture not found: {SourceTexturePath}");
			return;
		}

		EnsureDirectory(GeneratedFolder);
		EnsureDirectory("Assets/Dices/Prefabs");
		EnsureDirectory(FaceSpriteFolder);
		ConfigureTextureImporter(SourceTexturePath);

		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceTexturePath);
		if (texture == null)
		{
			Debug.LogError($"[DicePrefabBuilder] Failed to load texture: {SourceTexturePath}");
			return;
		}

		var faceRects = BuildFaceRects(texture);
		var atlas = BuildCleanAtlas(texture, faceRects);
		WriteAtlas(atlas, AtlasPath);
		WriteFaceSprites(atlas);
		ConfigureTextureImporter(AtlasPath);
		var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
		if (atlasTexture == null)
		{
			Debug.LogError($"[DicePrefabBuilder] Failed to load generated atlas: {AtlasPath}");
			return;
		}

		var mesh = CreateD6Mesh(atlasTexture);
		mesh.name = "D6MineMesh";
		var savedMesh = SaveOrUpdateMesh(mesh, MeshPath);

		var material = SaveOrUpdateMaterial(atlasTexture, MaterialPath);
		if (savedMesh == null || material == null)
			return;

		var root = new GameObject("Dice_d6_mine");
		try
		{
			var filter = root.AddComponent<MeshFilter>();
			filter.sharedMesh = savedMesh;

			var renderer = root.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = material;

			var collider = root.AddComponent<MeshCollider>();
			collider.sharedMesh = savedMesh;
			collider.convex = true;

			PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		}
		finally
		{
			Object.DestroyImmediate(root);
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log($"[DicePrefabBuilder] Built prefab: {PrefabPath}");
	}

	static Mesh CreateD6Mesh(Texture2D texture)
	{
		var vertices = new Vector3[24];
		var normals = new Vector3[24];
		var uvs = new Vector2[24];
		var triangles = new int[36];
		int vertex = 0;
		int triangle = 0;

		AddFace(1, Vector3.forward, Vector3.right, Vector3.up,
			ref vertex, ref triangle, vertices, normals, uvs, triangles, texture);
		AddFace(2, Vector3.up, Vector3.right, Vector3.forward,
			ref vertex, ref triangle, vertices, normals, uvs, triangles, texture);
		AddFace(3, Vector3.left, Vector3.forward, Vector3.up,
			ref vertex, ref triangle, vertices, normals, uvs, triangles, texture);
		AddFace(4, Vector3.right, Vector3.back, Vector3.up,
			ref vertex, ref triangle, vertices, normals, uvs, triangles, texture);
		AddFace(5, Vector3.down, Vector3.right, Vector3.back,
			ref vertex, ref triangle, vertices, normals, uvs, triangles, texture);
		AddFace(6, Vector3.back, Vector3.left, Vector3.up,
			ref vertex, ref triangle, vertices, normals, uvs, triangles, texture);

		var mesh = new Mesh
		{
			vertices = vertices,
			normals = normals,
			uv = uvs,
			triangles = triangles
		};
		mesh.RecalculateBounds();
		return mesh;
	}

	static void AddFace(int faceValue, Vector3 normal, Vector3 axisU, Vector3 axisV,
		ref int vertex, ref int triangle,
		Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles, Texture2D texture)
	{
		var center = normal * HalfSize;
		var u = axisU * HalfSize;
		var v = axisV * HalfSize;
		var uv = GetFaceUvRect(texture, faceValue);
		int start = vertex;

		vertices[vertex + 0] = center - u - v;
		vertices[vertex + 1] = center - u + v;
		vertices[vertex + 2] = center + u + v;
		vertices[vertex + 3] = center + u - v;

		for (int i = 0; i < 4; i++)
			normals[vertex + i] = normal;

		uvs[vertex + 0] = new Vector2(uv.xMin, uv.yMin);
		uvs[vertex + 1] = new Vector2(uv.xMin, uv.yMax);
		uvs[vertex + 2] = new Vector2(uv.xMax, uv.yMax);
		uvs[vertex + 3] = new Vector2(uv.xMax, uv.yMin);

		triangles[triangle + 0] = start + 0;
		triangles[triangle + 1] = start + 1;
		triangles[triangle + 2] = start + 2;
		triangles[triangle + 3] = start + 0;
		triangles[triangle + 4] = start + 2;
		triangles[triangle + 5] = start + 3;

		vertex += 4;
		triangle += 6;
	}

	static Rect GetFaceUvRect(Texture2D texture, int faceValue)
	{
		int index = faceValue - 1;
		int column = index % Columns;
		int rowFromTop = index / Columns;
		int tileX = column * OutputTileSize;
		int tileY = (Rows - 1 - rowFromTop) * OutputTileSize;
		return PixelRectToUv(texture, tileX, tileY, OutputTileSize, OutputTileSize);
	}

	static RectInt[] BuildFaceRects(Texture2D texture)
	{
		var rects = new RectInt[6];
		for (int face = 1; face <= 6; face++)
			rects[face - 1] = FindFaceSourceRect(texture, face);
		return rects;
	}

	static RectInt FindFaceSourceRect(Texture2D texture, int faceValue)
	{
		int tileWidth = texture.width / Columns;
		int tileHeight = texture.height / Rows;
		int index = faceValue - 1;
		int column = index % Columns;
		int rowFromTop = index / Columns;
		int tileX = column * tileWidth;
		int tileY = (Rows - 1 - rowFromTop) * tileHeight;

		int minX = tileWidth;
		int minY = tileHeight;
		int maxX = -1;
		int maxY = -1;

		for (int y = 0; y < tileHeight; y++)
		{
			for (int x = 0; x < tileWidth; x++)
			{
				var c = texture.GetPixel(tileX + x, tileY + y);
				if (IsWhiteBackground(c)) continue;

				if (x < minX) minX = x;
				if (y < minY) minY = y;
				if (x > maxX) maxX = x;
				if (y > maxY) maxY = y;
			}
		}

		if (maxX < 0 || maxY < 0)
			return new RectInt(tileX, tileY, tileWidth, tileHeight);

		const int pad = 2;
		minX = Mathf.Max(0, minX - pad);
		minY = Mathf.Max(0, minY - pad);
		maxX = Mathf.Min(tileWidth - 1, maxX + pad);
		maxY = Mathf.Min(tileHeight - 1, maxY + pad);

		return new RectInt(tileX + minX, tileY + minY, maxX - minX + 1, maxY - minY + 1);
	}

	static Texture2D BuildCleanAtlas(Texture2D source, RectInt[] faceRects)
	{
		var atlas = new Texture2D(OutputTileSize * Columns, OutputTileSize * Rows, TextureFormat.RGBA32, false);
		atlas.filterMode = FilterMode.Point;
		atlas.wrapMode = TextureWrapMode.Clamp;

		for (int face = 1; face <= 6; face++)
		{
			var sourceRect = faceRects[face - 1];
			int index = face - 1;
			int column = index % Columns;
			int rowFromTop = index / Columns;
			int dstX = column * OutputTileSize;
			int dstY = (Rows - 1 - rowFromTop) * OutputTileSize;

			for (int y = 0; y < OutputTileSize; y++)
			{
				for (int x = 0; x < OutputTileSize; x++)
				{
					float u = OutputTileSize <= 1 ? 0f : (float)x / (OutputTileSize - 1);
					float v = OutputTileSize <= 1 ? 0f : (float)y / (OutputTileSize - 1);
					int sx = sourceRect.x + Mathf.Clamp(Mathf.RoundToInt(u * (sourceRect.width - 1)), 0, sourceRect.width - 1);
					int sy = sourceRect.y + Mathf.Clamp(Mathf.RoundToInt(v * (sourceRect.height - 1)), 0, sourceRect.height - 1);
					var color = SampleFilledFacePixel(source, sourceRect, sx, sy);
					atlas.SetPixel(dstX + x, dstY + y, color);
				}
			}
		}

		atlas.Apply(false, false);
		return atlas;
	}

	static Color SampleFilledFacePixel(Texture2D source, RectInt rect, int sx, int sy)
	{
		var color = source.GetPixel(sx, sy);
		if (!IsWhiteBackground(color))
			return color;

		float centerX = rect.x + (rect.width - 1) * 0.5f;
		float centerY = rect.y + (rect.height - 1) * 0.5f;
		float dx = sx - centerX;
		float dy = sy - centerY;
		int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)));

		for (int i = steps; i >= 0; i--)
		{
			float t = steps == 0 ? 0f : (float)i / steps;
			int x = Mathf.Clamp(Mathf.RoundToInt(centerX + dx * t), rect.x, rect.xMax - 1);
			int y = Mathf.Clamp(Mathf.RoundToInt(centerY + dy * t), rect.y, rect.yMax - 1);
			color = source.GetPixel(x, y);
			if (!IsWhiteBackground(color))
				return color;
		}

		return source.GetPixel(
			Mathf.Clamp(Mathf.RoundToInt(centerX), rect.x, rect.xMax - 1),
			Mathf.Clamp(Mathf.RoundToInt(centerY), rect.y, rect.yMax - 1));
	}

	static void WriteAtlas(Texture2D atlas, string path)
	{
		File.WriteAllBytes(path, atlas.EncodeToPNG());
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
	}

	static void WriteFaceSprites(Texture2D atlas)
	{
		for (int face = 1; face <= 6; face++)
		{
			int index = face - 1;
			int column = index % Columns;
			int rowFromTop = index / Columns;
			int srcX = column * OutputTileSize;
			int srcY = (Rows - 1 - rowFromTop) * OutputTileSize;

			var faceTexture = new Texture2D(OutputTileSize, OutputTileSize, TextureFormat.RGBA32, false);
			faceTexture.filterMode = FilterMode.Point;
			faceTexture.wrapMode = TextureWrapMode.Clamp;
			faceTexture.SetPixels(atlas.GetPixels(srcX, srcY, OutputTileSize, OutputTileSize));
			faceTexture.Apply(false, false);

			string path = $"{FaceSpriteFolder}/face{face}.png";
			File.WriteAllBytes(path, faceTexture.EncodeToPNG());
			Object.DestroyImmediate(faceTexture);
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			ConfigureFaceSpriteImporter(path);
		}
	}

	static void ConfigureFaceSpriteImporter(string path)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
			return;

		bool changed = false;
		if (importer.textureType != TextureImporterType.Sprite)
		{
			importer.textureType = TextureImporterType.Sprite;
			changed = true;
		}
		if (importer.spritePixelsPerUnit != 100f)
		{
			importer.spritePixelsPerUnit = 100f;
			changed = true;
		}
		if (importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			changed = true;
		}
		if (importer.textureCompression != TextureImporterCompression.Uncompressed)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			changed = true;
		}
		if (importer.mipmapEnabled)
		{
			importer.mipmapEnabled = false;
			changed = true;
		}
		if (changed)
			importer.SaveAndReimport();
	}

	static bool IsWhiteBackground(Color color)
	{
		if (color.a <= 0.05f)
			return true;

		float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
		float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
		return min >= BackgroundValueThreshold
		    && max - min <= BackgroundSaturationThreshold;
	}

	static Rect PixelRectToUv(Texture2D texture, int x, int y, int width, int height)
	{
		return new Rect(
			(float)x / texture.width,
			(float)y / texture.height,
			(float)width / texture.width,
			(float)height / texture.height);
	}

	static Mesh SaveOrUpdateMesh(Mesh source, string path)
	{
		var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
		if (existing == null)
		{
			AssetDatabase.CreateAsset(source, path);
			return source;
		}

		EditorUtility.CopySerialized(source, existing);
		existing.name = source.name;
		EditorUtility.SetDirty(existing);
		return existing;
	}

	static Material SaveOrUpdateMaterial(Texture2D texture, string path)
	{
		var material = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (material == null)
		{
			var shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null)
				shader = Shader.Find("Standard");
			if (shader == null)
			{
				Debug.LogError("[DicePrefabBuilder] No compatible Lit shader found.");
				return null;
			}

			material = new Material(shader) { name = "D6Mine" };
			AssetDatabase.CreateAsset(material, path);
		}

		if (material.HasProperty("_BaseMap"))
			material.SetTexture("_BaseMap", texture);
		if (material.HasProperty("_MainTex"))
			material.SetTexture("_MainTex", texture);
		if (material.HasProperty("_BaseColor"))
			material.SetColor("_BaseColor", Color.white);
		if (material.HasProperty("_Color"))
			material.SetColor("_Color", Color.white);
		if (material.HasProperty("_Smoothness"))
			material.SetFloat("_Smoothness", 0.25f);
		if (material.HasProperty("_Metallic"))
			material.SetFloat("_Metallic", 0f);
		if (material.HasProperty("_Cull"))
			material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

		EditorUtility.SetDirty(material);
		return material;
	}

	static void ConfigureTextureImporter(string path)
	{
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
		{
			Debug.LogWarning($"[DicePrefabBuilder] Texture importer not found: {path}");
			return;
		}

		bool changed = false;
		if (importer.textureType != TextureImporterType.Default)
		{
			importer.textureType = TextureImporterType.Default;
			changed = true;
		}
		if (!importer.sRGBTexture)
		{
			importer.sRGBTexture = true;
			changed = true;
		}
		if (importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			changed = true;
		}
		if (importer.textureCompression != TextureImporterCompression.Uncompressed)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			changed = true;
		}
		if (importer.mipmapEnabled)
		{
			importer.mipmapEnabled = false;
			changed = true;
		}
		if (!importer.isReadable)
		{
			importer.isReadable = true;
			changed = true;
		}

		if (changed)
			importer.SaveAndReimport();
	}

	static void EnsureDirectory(string path)
	{
		if (AssetDatabase.IsValidFolder(path))
			return;

		var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
		var name = Path.GetFileName(path);
		if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
			return;

		EnsureDirectory(parent);
		if (!AssetDatabase.IsValidFolder(path))
			AssetDatabase.CreateFolder(parent, name);
	}
}
