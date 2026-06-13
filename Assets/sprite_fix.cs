#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class LooseSpriteSheetRectifierWindow : EditorWindow
{
	private enum PlacementMode
	{
		PreserveRoughCellPosition,
		BottomCenterContent,
		BottomCenterBodyAnchor
	}

	private enum SourceLayoutMode
	{
		AutoDetectContent,
		FixedGrid
	}

	private struct FrameInfo
	{
		public int row;
		public int column;
		public int index;
		public RectInt sourceCell;
		public RectInt contentBounds;
		public RectInt anchorBounds;

		public bool HasContent
		{
			get
			{
				return contentBounds.width > 0 && contentBounds.height > 0;
			}
		}
	}

	private struct ComponentInfo
	{
		public RectInt bounds;
		public int pixelCount;

		public float CenterX
		{
			get
			{
				return bounds.xMin + (bounds.width - 1) * 0.5f;
			}
		}

		public float CenterY
		{
			get
			{
				return bounds.yMin + (bounds.height - 1) * 0.5f;
			}
		}
	}

	private sealed class DetectedRow
	{
		public readonly List<ComponentInfo> anchors = new List<ComponentInfo>();
		public float centerY;
		public int yMin;
		public int yMax;
	}

	private Texture2D sourceTexture;
	private string lastBuildError;

	private SourceLayoutMode sourceLayoutMode = SourceLayoutMode.AutoDetectContent;

	private int columns = 8;
	private int rows = 8;

	private bool autoOutputCellSize = true;
	private int outputCellSize = 192;
	private int padding = 8;

	private int whiteThreshold = 245;
	private int minComponentPixels = 20;
	private int minAnchorPixels = 2500;
	private int minAnchorSize = 45;
	private int expectedAutoColumns = 8;
	private int rowTolerance = 80;
	private int rowComponentTolerance = 100;
	private int looseComponentJoinPadding = 24;
	private int pixelsPerUnit = 100;
	private string frameYOffsetOverrides = string.Empty;
	private string horizontalFlipFrames = string.Empty;

	private Vector2 spritePivot = new Vector2(0.5f, 0.0f);

	private bool createAnimationClip = true;
	private float frameRate = 12f;

	private PlacementMode placementMode = PlacementMode.BottomCenterBodyAnchor;

	[MenuItem("Tools/2D/Rectify Loose Sprite Sheet")]
	public static void Open()
	{
		LooseSpriteSheetRectifierWindow window =
			GetWindow<LooseSpriteSheetRectifierWindow>("Sprite Sheet Rectifier");

		if (Selection.activeObject is Texture2D selectedTexture)
		{
			window.sourceTexture = selectedTexture;
		}
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Loose Sprite Sheet Rectifier", EditorStyles.boldLabel);

		sourceTexture = EditorGUILayout.ObjectField(
			"Source Texture",
			sourceTexture,
			typeof(Texture2D),
			false
		) as Texture2D;

		EditorGUILayout.Space();

		sourceLayoutMode = (SourceLayoutMode)EditorGUILayout.EnumPopup(
			"Source Layout Mode",
			sourceLayoutMode
		);

		if (sourceLayoutMode == SourceLayoutMode.FixedGrid)
		{
			columns = EditorGUILayout.IntSlider("Source Columns", columns, 1, 16);
			rows = EditorGUILayout.IntSlider("Source Rows", rows, 1, 16);
		}
		else
		{
			EditorGUILayout.HelpBox(
				"AutoDetectContent: 흰 배경을 제외한 픽셀 덩어리에서 본체 프레임을 찾고, 무기/파편처럼 분리된 작은 덩어리를 가까운 프레임에 병합합니다.",
				MessageType.Info
			);

			minComponentPixels = EditorGUILayout.IntSlider("Min Component Pixels", minComponentPixels, 1, 500);
			minAnchorPixels = EditorGUILayout.IntSlider("Min Body Pixels", minAnchorPixels, 500, 10000);
			minAnchorSize = EditorGUILayout.IntSlider("Min Body Size", minAnchorSize, 8, 128);
			expectedAutoColumns = EditorGUILayout.IntSlider("Expected Columns", expectedAutoColumns, 0, 16);
			rowTolerance = EditorGUILayout.IntSlider("Row Tolerance", rowTolerance, 16, 128);
			rowComponentTolerance = EditorGUILayout.IntSlider("Row Component Tolerance", rowComponentTolerance, 16, 160);
			looseComponentJoinPadding = EditorGUILayout.IntSlider("Loose Join Padding", looseComponentJoinPadding, 0, 96);
		}

		placementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", placementMode);

		EditorGUILayout.HelpBox(
			"PreserveRoughCellPosition: 원본의 대략적인 셀 내부 위치를 유지합니다. AI 시트 보정 1차 시도에 적합합니다.\n" +
			"BottomCenterContent: 각 프레임의 실제 픽셀 박스를 잘라서 아래 중앙에 맞춥니다. 피벗 안정화에는 좋지만 원래 연출 위치가 바뀔 수 있습니다.\n" +
			"BottomCenterBodyAnchor: 분리된 무기/파편은 포함하되 본체의 바닥과 중앙을 기준으로 정렬합니다.",
			MessageType.Info
		);

		whiteThreshold = EditorGUILayout.IntSlider("White Background Threshold", whiteThreshold, 200, 255);
		padding = EditorGUILayout.IntSlider("Output Padding", padding, 0, 64);
		frameYOffsetOverrides = EditorGUILayout.TextField("Frame Y Offsets", frameYOffsetOverrides);
		EditorGUILayout.HelpBox(
			"Frame Y Offsets 예: 3:-12,4:-12,10-15:8  /  양수는 위로, 음수는 아래로 이동합니다.",
			MessageType.None
		);
		horizontalFlipFrames = EditorGUILayout.TextField("Horizontal Flip Frames", horizontalFlipFrames);
		EditorGUILayout.HelpBox(
			"Horizontal Flip Frames 예: 3,4,10-15  /  지정한 프레임만 좌우반전합니다.",
			MessageType.None
		);

		autoOutputCellSize = EditorGUILayout.Toggle("Auto Output Cell Size", autoOutputCellSize);

		using (new EditorGUI.DisabledScope(autoOutputCellSize))
		{
			outputCellSize = EditorGUILayout.IntField("Output Cell Size", outputCellSize);
		}

		pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", pixelsPerUnit);
		spritePivot = EditorGUILayout.Vector2Field("Sprite Pivot", spritePivot);

		createAnimationClip = EditorGUILayout.Toggle("Create Animation Clip", createAnimationClip);

		using (new EditorGUI.DisabledScope(!createAnimationClip))
		{
			frameRate = EditorGUILayout.FloatField("Animation Frame Rate", frameRate);
		}

		EditorGUILayout.Space();

		if (GUILayout.Button("Generate Rectified Sprite Sheet"))
		{
			Generate();
		}
	}

	private void Generate()
	{
		if (sourceTexture == null)
		{
			EditorUtility.DisplayDialog("Error", "Source Texture를 지정하세요.", "OK");
			return;
		}

		string sourceAssetPath = AssetDatabase.GetAssetPath(sourceTexture);

		if (string.IsNullOrEmpty(sourceAssetPath))
		{
			EditorUtility.DisplayDialog("Error", "Project 안에 있는 Texture asset만 처리할 수 있습니다.", "OK");
			return;
		}

		TextureImporter sourceImporter = AssetImporter.GetAtPath(sourceAssetPath) as TextureImporter;

		if (sourceImporter == null)
		{
			EditorUtility.DisplayDialog("Error", "TextureImporter를 찾을 수 없습니다.", "OK");
			return;
		}

		bool oldReadable = sourceImporter.isReadable;

		try
		{
			if (!oldReadable)
			{
				sourceImporter.isReadable = true;
				sourceImporter.SaveAndReimport();
			}

			Texture2D readableSource = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);

			lastBuildError = null;

			List<FrameInfo> frames = BuildFrames(
				readableSource,
				out int frameColumns,
				out int frameRows
			);

			if (frames.Count == 0)
			{
				string message = string.IsNullOrEmpty(lastBuildError)
					? "처리할 프레임을 찾지 못했습니다."
					: lastBuildError;

				EditorUtility.DisplayDialog("Error", message, "OK");
				return;
			}

			int finalCellSize = autoOutputCellSize
				? CalculateAutoCellSize(frames)
				: Mathf.Max(16, outputCellSize);

			Texture2D outputTexture = BuildOutputTexture(
				readableSource,
				frames,
				finalCellSize,
				frameColumns,
				frameRows
			);

			string folder = Path.GetDirectoryName(sourceAssetPath);
			string sourceName = Path.GetFileNameWithoutExtension(sourceAssetPath);
			string outputAssetPath = AssetDatabase.GenerateUniqueAssetPath(
				$"{folder}/{sourceName}_rectified.png"
			);

			File.WriteAllBytes(Path.GetFullPath(outputAssetPath), outputTexture.EncodeToPNG());
			AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);

			ConfigureOutputImporter(outputAssetPath, finalCellSize, frameColumns, frameRows);
			ApplySpriteRects(outputAssetPath, finalCellSize, frameColumns, frameRows);

			if (createAnimationClip)
			{
				CreateAnimationClip(outputAssetPath);
			}

			AssetDatabase.Refresh();

			Texture2D generatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputAssetPath);
			Selection.activeObject = generatedTexture;

			EditorUtility.DisplayDialog(
				"Done",
				$"생성 완료:\n{outputAssetPath}\n\nFrames: {frames.Count} ({frameColumns}x{frameRows})\nCell Size: {finalCellSize}x{finalCellSize}",
				"OK"
			);
		}
		finally
		{
			if (sourceImporter != null && sourceImporter.isReadable != oldReadable)
			{
				sourceImporter.isReadable = oldReadable;
				sourceImporter.SaveAndReimport();
			}
		}
	}

	private List<FrameInfo> BuildFrames(Texture2D source, out int frameColumns, out int frameRows)
	{
		if (sourceLayoutMode == SourceLayoutMode.AutoDetectContent)
		{
			return BuildAutoDetectedFrames(source, out frameColumns, out frameRows);
		}

		return BuildFixedGridFrames(source, out frameColumns, out frameRows);
	}

	private List<FrameInfo> BuildFixedGridFrames(Texture2D source, out int frameColumns, out int frameRows)
	{
		List<FrameInfo> frames = new List<FrameInfo>(columns * rows);
		frameColumns = columns;
		frameRows = rows;

		float sourceCellWidth = source.width / (float)columns;
		float sourceCellHeight = source.height / (float)rows;

		for (int row = 0; row < rows; row++)
		{
			for (int column = 0; column < columns; column++)
			{
				int xMin = Mathf.RoundToInt(column * sourceCellWidth);
				int xMax = Mathf.RoundToInt((column + 1) * sourceCellWidth);

				int topYMin = Mathf.RoundToInt(row * sourceCellHeight);
				int topYMax = Mathf.RoundToInt((row + 1) * sourceCellHeight);

				int yMin = source.height - topYMax;
				int yMax = source.height - topYMin;

				xMin = Mathf.Clamp(xMin, 0, source.width);
				xMax = Mathf.Clamp(xMax, 0, source.width);
				yMin = Mathf.Clamp(yMin, 0, source.height);
				yMax = Mathf.Clamp(yMax, 0, source.height);

				RectInt sourceCell = new RectInt(
					xMin,
					yMin,
					Mathf.Max(0, xMax - xMin),
					Mathf.Max(0, yMax - yMin)
				);

				RectInt contentBounds = FindContentBounds(source, sourceCell);
				FrameInfo frame = new FrameInfo
				{
					row = row,
					column = column,
					index = row * columns + column,
					sourceCell = sourceCell,
					contentBounds = contentBounds,
					anchorBounds = contentBounds
				};

				frames.Add(frame);
			}
		}

		return frames;
	}

	private List<FrameInfo> BuildAutoDetectedFrames(
		Texture2D source,
		out int frameColumns,
		out int frameRows
	)
	{
		Color32[] pixels = source.GetPixels32();
		bool[] foregroundMask = BuildForegroundMask(pixels);
		List<ComponentInfo> components = FindComponents(
			foregroundMask,
			source.width,
			source.height
		);

		List<ComponentInfo> anchors = components
			.Where(IsAnchorComponent)
			.OrderBy(component => component.CenterY)
			.ThenBy(component => component.CenterX)
			.ToList();

		List<DetectedRow> detectedRows = BuildDetectedRows(anchors);

		if (
			expectedAutoColumns > 0 &&
			anchors.Count >= expectedAutoColumns &&
			anchors.Count % expectedAutoColumns == 0
		)
		{
			detectedRows = BuildRowsByExpectedColumns(anchors, expectedAutoColumns);
		}

		if (detectedRows.Count == 0)
		{
			lastBuildError = "Auto Detect에서 본체 프레임을 찾지 못했습니다. Min Body Pixels 또는 Min Body Size를 낮춰보세요.";
			frameColumns = 0;
			frameRows = 0;
			return new List<FrameInfo>();
		}

		detectedRows = detectedRows
			.OrderByDescending(row => row.centerY)
			.ToList();

		foreach (DetectedRow detectedRow in detectedRows)
		{
			detectedRow.anchors.Sort((left, right) => left.CenterX.CompareTo(right.CenterX));
		}

		frameRows = detectedRows.Count;
		frameColumns = detectedRows.Max(row => row.anchors.Count);
		int expectedColumns = frameColumns;

		if (frameColumns <= 0 || detectedRows.Any(row => row.anchors.Count != expectedColumns))
		{
			string rowCounts = string.Join(
				", ",
				detectedRows.Select(row => row.anchors.Count.ToString()).ToArray()
			);

			Debug.LogWarning($"Auto Detect frame count mismatch. Row counts: {rowCounts}");
			lastBuildError = $"행마다 감지된 프레임 수가 다릅니다.\nRow counts: {rowCounts}\n\nMin Body Pixels 또는 Row Tolerance를 조정하세요.";

			frameColumns = 0;
			frameRows = 0;
			return new List<FrameInfo>();
		}

		List<List<RectInt>> frameBounds = BuildAnchorBounds(detectedRows);
		AssignLooseComponentsToFrames(components, anchors, detectedRows, frameBounds);

		List<FrameInfo> frames = new List<FrameInfo>(frameColumns * frameRows);

		for (int row = 0; row < frameRows; row++)
		{
			for (int column = 0; column < frameColumns; column++)
			{
				RectInt contentBounds = frameBounds[row][column];
				RectInt sourceCell = ExpandRect(
					contentBounds,
					padding,
					source.width,
					source.height
				);

				frames.Add(new FrameInfo
				{
					row = row,
					column = column,
					index = row * frameColumns + column,
					sourceCell = sourceCell,
					contentBounds = contentBounds,
					anchorBounds = detectedRows[row].anchors[column].bounds
				});
			}
		}

		Debug.Log(
			$"Auto Detect Sprite Sheet: {frameColumns}x{frameRows}, frames={frames.Count}, components={components.Count}, anchors={anchors.Count}"
		);

		return frames;
	}

	private bool[] BuildForegroundMask(Color32[] pixels)
	{
		bool[] foregroundMask = new bool[pixels.Length];

		for (int i = 0; i < pixels.Length; i++)
		{
			foregroundMask[i] = IsForeground(pixels[i]);
		}

		return foregroundMask;
	}

	private List<ComponentInfo> FindComponents(bool[] foregroundMask, int width, int height)
	{
		List<ComponentInfo> components = new List<ComponentInfo>();
		bool[] visited = new bool[foregroundMask.Length];
		Queue<int> pendingPixels = new Queue<int>();

		for (int y = 0; y < height; y++)
		{
			int rowOffset = y * width;

			for (int x = 0; x < width; x++)
			{
				int startIndex = rowOffset + x;

				if (!foregroundMask[startIndex] || visited[startIndex])
				{
					continue;
				}

				ComponentInfo component = FloodFillComponent(
					foregroundMask,
					visited,
					pendingPixels,
					width,
					height,
					startIndex
				);

				if (component.pixelCount >= minComponentPixels)
				{
					components.Add(component);
				}
			}
		}

		return components;
	}

	private ComponentInfo FloodFillComponent(
		bool[] foregroundMask,
		bool[] visited,
		Queue<int> pendingPixels,
		int width,
		int height,
		int startIndex
	)
	{
		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;
		int pixelCount = 0;

		visited[startIndex] = true;
		pendingPixels.Clear();
		pendingPixels.Enqueue(startIndex);

		while (pendingPixels.Count > 0)
		{
			int pixelIndex = pendingPixels.Dequeue();
			int x = pixelIndex % width;
			int y = pixelIndex / width;

			pixelCount++;
			minX = Mathf.Min(minX, x);
			minY = Mathf.Min(minY, y);
			maxX = Mathf.Max(maxX, x);
			maxY = Mathf.Max(maxY, y);

			EnqueueConnectedPixel(foregroundMask, visited, pendingPixels, width, height, x + 1, y);
			EnqueueConnectedPixel(foregroundMask, visited, pendingPixels, width, height, x - 1, y);
			EnqueueConnectedPixel(foregroundMask, visited, pendingPixels, width, height, x, y + 1);
			EnqueueConnectedPixel(foregroundMask, visited, pendingPixels, width, height, x, y - 1);
		}

		return new ComponentInfo
		{
			bounds = new RectInt(
				minX,
				minY,
				maxX - minX + 1,
				maxY - minY + 1
			),
			pixelCount = pixelCount
		};
	}

	private void EnqueueConnectedPixel(
		bool[] foregroundMask,
		bool[] visited,
		Queue<int> pendingPixels,
		int width,
		int height,
		int x,
		int y
	)
	{
		if (x < 0 || y < 0 || x >= width || y >= height)
		{
			return;
		}

		int index = y * width + x;

		if (!foregroundMask[index] || visited[index])
		{
			return;
		}

		visited[index] = true;
		pendingPixels.Enqueue(index);
	}

	private bool IsAnchorComponent(ComponentInfo component)
	{
		return
			component.pixelCount >= minAnchorPixels &&
			component.bounds.width >= minAnchorSize &&
			component.bounds.height >= minAnchorSize;
	}

	private List<DetectedRow> BuildDetectedRows(List<ComponentInfo> anchors)
	{
		List<DetectedRow> detectedRows = new List<DetectedRow>();

		foreach (ComponentInfo anchor in anchors)
		{
			DetectedRow targetRow = FindBestDetectedRow(anchor, detectedRows);

			if (targetRow == null)
			{
				targetRow = new DetectedRow
				{
					centerY = anchor.CenterY,
					yMin = anchor.bounds.yMin,
					yMax = anchor.bounds.yMax
				};
				detectedRows.Add(targetRow);
			}

			targetRow.anchors.Add(anchor);
			targetRow.centerY = targetRow.anchors.Average(component => component.CenterY);
			targetRow.yMin = Mathf.Min(targetRow.yMin, anchor.bounds.yMin);
			targetRow.yMax = Mathf.Max(targetRow.yMax, anchor.bounds.yMax);
		}

		return detectedRows;
	}

	private List<DetectedRow> BuildRowsByExpectedColumns(
		List<ComponentInfo> anchors,
		int expectedColumns
	)
	{
		List<ComponentInfo> sortedAnchors = anchors
			.OrderByDescending(anchor => anchor.CenterY)
			.ThenBy(anchor => anchor.CenterX)
			.ToList();

		List<DetectedRow> detectedRows = new List<DetectedRow>(
			sortedAnchors.Count / expectedColumns
		);

		for (int rowStart = 0; rowStart < sortedAnchors.Count; rowStart += expectedColumns)
		{
			DetectedRow detectedRow = new DetectedRow();

			for (int index = rowStart; index < rowStart + expectedColumns; index++)
			{
				ComponentInfo anchor = sortedAnchors[index];
				detectedRow.anchors.Add(anchor);

				if (detectedRow.anchors.Count == 1)
				{
					detectedRow.yMin = anchor.bounds.yMin;
					detectedRow.yMax = anchor.bounds.yMax;
				}
				else
				{
					detectedRow.yMin = Mathf.Min(detectedRow.yMin, anchor.bounds.yMin);
					detectedRow.yMax = Mathf.Max(detectedRow.yMax, anchor.bounds.yMax);
				}
			}

			detectedRow.centerY = detectedRow.anchors.Average(anchor => anchor.CenterY);
			detectedRows.Add(detectedRow);
		}

		return detectedRows;
	}

	private DetectedRow FindBestDetectedRow(ComponentInfo anchor, List<DetectedRow> detectedRows)
	{
		DetectedRow bestRow = null;
		int bestOverlap = 0;
		float bestDistance = float.MaxValue;

		int expandedYMin = anchor.bounds.yMin - rowTolerance;
		int expandedYMax = anchor.bounds.yMax + rowTolerance;

		foreach (DetectedRow detectedRow in detectedRows)
		{
			int overlap = CalculateRangeOverlap(
				expandedYMin,
				expandedYMax,
				detectedRow.yMin,
				detectedRow.yMax
			);
			float distance = Mathf.Abs(anchor.CenterY - detectedRow.centerY);

			if (overlap <= 0 && distance > rowTolerance)
			{
				continue;
			}

			if (overlap > bestOverlap || (overlap == bestOverlap && distance < bestDistance))
			{
				bestRow = detectedRow;
				bestOverlap = overlap;
				bestDistance = distance;
			}
		}

		return bestRow;
	}

	private int CalculateRangeOverlap(int firstMin, int firstMax, int secondMin, int secondMax)
	{
		return Mathf.Max(0, Mathf.Min(firstMax, secondMax) - Mathf.Max(firstMin, secondMin));
	}

	private List<List<RectInt>> BuildAnchorBounds(List<DetectedRow> detectedRows)
	{
		List<List<RectInt>> frameBounds = new List<List<RectInt>>(detectedRows.Count);

		foreach (DetectedRow detectedRow in detectedRows)
		{
			List<RectInt> rowBounds = new List<RectInt>(detectedRow.anchors.Count);

			foreach (ComponentInfo anchor in detectedRow.anchors)
			{
				rowBounds.Add(anchor.bounds);
			}

			frameBounds.Add(rowBounds);
		}

		return frameBounds;
	}

	private void AssignLooseComponentsToFrames(
		List<ComponentInfo> components,
		List<ComponentInfo> anchors,
		List<DetectedRow> detectedRows,
		List<List<RectInt>> frameBounds
	)
	{
		HashSet<RectInt> anchorBounds = new HashSet<RectInt>(
			anchors.Select(anchor => anchor.bounds)
		);

		foreach (ComponentInfo component in components)
		{
			if (anchorBounds.Contains(component.bounds))
			{
				continue;
			}

			if (!TryFindFrameForLooseComponent(
				component,
				detectedRows,
				out int targetRow,
				out int targetColumn
			))
			{
				continue;
			}

			frameBounds[targetRow][targetColumn] = UnionRects(
				frameBounds[targetRow][targetColumn],
				component.bounds
			);
		}
	}

	private bool TryFindFrameForLooseComponent(
		ComponentInfo component,
		List<DetectedRow> detectedRows,
		out int targetRow,
		out int targetColumn
	)
	{
		targetRow = -1;
		targetColumn = -1;
		float bestRowDistance = float.MaxValue;

		for (int row = 0; row < detectedRows.Count; row++)
		{
			float rowDistance = Mathf.Abs(component.CenterY - detectedRows[row].centerY);

			if (rowDistance > rowComponentTolerance || rowDistance >= bestRowDistance)
			{
				continue;
			}

			bestRowDistance = rowDistance;
			targetRow = row;
		}

		if (targetRow < 0)
		{
			return false;
		}

		List<ComponentInfo> rowAnchors = detectedRows[targetRow].anchors;
		float bestOverlapDistance = float.MaxValue;

		for (int column = 0; column < rowAnchors.Count; column++)
		{
			RectInt expandedAnchor = ExpandRect(
				rowAnchors[column].bounds,
				looseComponentJoinPadding,
				int.MaxValue,
				int.MaxValue
			);

			if (!expandedAnchor.Overlaps(component.bounds))
			{
				continue;
			}

			float distance = Mathf.Abs(component.CenterX - rowAnchors[column].CenterX);

			if (distance < bestOverlapDistance)
			{
				bestOverlapDistance = distance;
				targetColumn = column;
			}
		}

		if (targetColumn >= 0)
		{
			return true;
		}

		for (int column = 0; column < rowAnchors.Count; column++)
		{
			if (component.CenterX <= rowAnchors[column].CenterX)
			{
				targetColumn = column;
				return true;
			}
		}

		targetColumn = rowAnchors.Count - 1;
		return true;
	}

	private RectInt UnionRects(RectInt left, RectInt right)
	{
		int xMin = Mathf.Min(left.xMin, right.xMin);
		int yMin = Mathf.Min(left.yMin, right.yMin);
		int xMax = Mathf.Max(left.xMax, right.xMax);
		int yMax = Mathf.Max(left.yMax, right.yMax);

		return new RectInt(
			xMin,
			yMin,
			xMax - xMin,
			yMax - yMin
		);
	}

	private RectInt ExpandRect(RectInt rect, int amount, int maxWidth, int maxHeight)
	{
		int xMin = Mathf.Max(0, rect.xMin - amount);
		int yMin = Mathf.Max(0, rect.yMin - amount);
		int xMax = Mathf.Min(maxWidth, rect.xMax + amount);
		int yMax = Mathf.Min(maxHeight, rect.yMax + amount);

		return new RectInt(
			xMin,
			yMin,
			Mathf.Max(0, xMax - xMin),
			Mathf.Max(0, yMax - yMin)
		);
	}

	private RectInt FindContentBounds(Texture2D source, RectInt sourceCell)
	{
		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;

		for (int y = sourceCell.yMin; y < sourceCell.yMax; y++)
		{
			for (int x = sourceCell.xMin; x < sourceCell.xMax; x++)
			{
				Color32 pixel = source.GetPixel(x, y);

				if (!IsForeground(pixel))
				{
					continue;
				}

				minX = Mathf.Min(minX, x);
				minY = Mathf.Min(minY, y);
				maxX = Mathf.Max(maxX, x);
				maxY = Mathf.Max(maxY, y);
			}
		}

		if (minX == int.MaxValue)
		{
			return new RectInt(sourceCell.xMin, sourceCell.yMin, 0, 0);
		}

		return new RectInt(
			minX,
			minY,
			maxX - minX + 1,
			maxY - minY + 1
		);
	}

	private bool IsForeground(Color32 pixel)
	{
		if (pixel.a <= 5)
		{
			return false;
		}

		bool isWhiteBackground =
			pixel.r >= whiteThreshold &&
			pixel.g >= whiteThreshold &&
			pixel.b >= whiteThreshold;

		return !isWhiteBackground;
	}

	private int CalculateAutoCellSize(List<FrameInfo> frames)
	{
		if (placementMode == PlacementMode.BottomCenterBodyAnchor)
		{
			return CalculateBodyAnchorCellSize(frames) + CalculateManualYOffsetPadding(frames);
		}

		int maxWidth = 1;
		int maxHeight = 1;

		foreach (FrameInfo frame in frames)
		{
			RectInt rect;

			if (placementMode == PlacementMode.PreserveRoughCellPosition)
			{
				rect = frame.sourceCell;
			}
			else
			{
				if (!frame.HasContent)
				{
					continue;
				}

				rect = frame.contentBounds;
			}

			maxWidth = Mathf.Max(maxWidth, rect.width);
			maxHeight = Mathf.Max(maxHeight, rect.height);
		}

		int required = Mathf.Max(maxWidth, maxHeight) + padding * 2 + CalculateManualYOffsetPadding(frames);
		return RoundUpToMultiple(required, 16);
	}

	private int CalculateManualYOffsetPadding(List<FrameInfo> frames)
	{
		int maxOffset = 0;

		foreach (FrameInfo frame in frames)
		{
			maxOffset = Mathf.Max(maxOffset, Mathf.Abs(GetManualFrameYOffset(frame.index)));
		}

		return maxOffset;
	}

	private int CalculateBodyAnchorCellSize(List<FrameInfo> frames)
	{
		int maxLeft = 1;
		int maxRight = 1;
		int maxBelow = 0;
		int maxAbove = 1;

		foreach (FrameInfo frame in frames)
		{
			if (!frame.HasContent)
			{
				continue;
			}

			float anchorCenterX = frame.anchorBounds.xMin + frame.anchorBounds.width * 0.5f;

			maxLeft = Mathf.Max(maxLeft, Mathf.CeilToInt(anchorCenterX - frame.contentBounds.xMin));
			maxRight = Mathf.Max(maxRight, Mathf.CeilToInt(frame.contentBounds.xMax - anchorCenterX));
			maxBelow = Mathf.Max(maxBelow, Mathf.Max(0, frame.anchorBounds.yMin - frame.contentBounds.yMin));
			maxAbove = Mathf.Max(maxAbove, Mathf.Max(0, frame.contentBounds.yMax - frame.anchorBounds.yMin));
		}

		int requiredWidth = Mathf.Max(maxLeft, maxRight) * 2 + padding * 2;
		int requiredHeight = maxBelow + maxAbove + padding * 2;

		return RoundUpToMultiple(Mathf.Max(requiredWidth, requiredHeight), 16);
	}

	private int RoundUpToMultiple(int value, int multiple)
	{
		if (multiple <= 0)
		{
			return value;
		}

		return ((value + multiple - 1) / multiple) * multiple;
	}

	private Texture2D BuildOutputTexture(
		Texture2D source,
		List<FrameInfo> frames,
		int cellSize,
		int frameColumns,
		int frameRows
	)
	{
		int outputWidth = frameColumns * cellSize;
		int outputHeight = frameRows * cellSize;

		Texture2D output = new Texture2D(
			outputWidth,
			outputHeight,
			TextureFormat.RGBA32,
			false
		);

		Color32[] clearPixels = new Color32[outputWidth * outputHeight];

		for (int i = 0; i < clearPixels.Length; i++)
		{
			clearPixels[i] = new Color32(0, 0, 0, 0);
		}

		output.SetPixels32(clearPixels);

		int bodyAnchorBaselineY = padding + CalculateMaxBelowBodyAnchor(frames);

		foreach (FrameInfo frame in frames)
		{
			CopyFrame(source, output, frame, cellSize, frameRows, bodyAnchorBaselineY);
		}

		output.Apply(false, false);
		return output;
	}

	private void CopyFrame(
		Texture2D source,
		Texture2D output,
		FrameInfo frame,
		int cellSize,
		int frameRows,
		int bodyAnchorBaselineY
	)
	{
		if (!frame.HasContent)
		{
			return;
		}

		RectInt copyRect;
		int offsetX;
		int offsetY;

		if (placementMode == PlacementMode.PreserveRoughCellPosition)
		{
			copyRect = frame.sourceCell;
			offsetX = Mathf.RoundToInt((cellSize - frame.sourceCell.width) * 0.5f);
			offsetY = Mathf.RoundToInt((cellSize - frame.sourceCell.height) * 0.5f);
		}
		else if (placementMode == PlacementMode.BottomCenterBodyAnchor)
		{
			copyRect = frame.contentBounds;

			float anchorCenterX = frame.anchorBounds.xMin + frame.anchorBounds.width * 0.5f;

			offsetX = Mathf.RoundToInt(cellSize * 0.5f - (anchorCenterX - copyRect.xMin));
			offsetY = bodyAnchorBaselineY - (frame.anchorBounds.yMin - copyRect.yMin);
		}
		else
		{
			copyRect = frame.contentBounds;
			offsetX = Mathf.RoundToInt((cellSize - frame.contentBounds.width) * 0.5f);
			offsetY = padding;
		}

		int destinationCellX = frame.column * cellSize;
		int destinationCellY = (frameRows - 1 - frame.row) * cellSize;
		int manualYOffset = GetManualFrameYOffset(frame.index);
		bool flipHorizontally = ShouldFlipHorizontally(frame.index);

		for (int y = copyRect.yMin; y < copyRect.yMax; y++)
		{
			for (int x = copyRect.xMin; x < copyRect.xMax; x++)
			{
				Color32 pixel = source.GetPixel(x, y);

				if (!IsForeground(pixel))
				{
					continue;
				}

				int sourceRelativeX = x - copyRect.xMin;
				int destinationRelativeX = flipHorizontally
					? copyRect.width - 1 - sourceRelativeX
					: sourceRelativeX;

				int destinationX = destinationCellX + offsetX + destinationRelativeX;
				int destinationY = destinationCellY + offsetY + manualYOffset + (y - copyRect.yMin);

				if (
					destinationX < 0 ||
					destinationY < 0 ||
					destinationX >= output.width ||
					destinationY >= output.height
				)
				{
					continue;
				}

				output.SetPixel(destinationX, destinationY, pixel);
			}
		}
	}

	private int CalculateMaxBelowBodyAnchor(List<FrameInfo> frames)
	{
		int maxBelow = 0;

		foreach (FrameInfo frame in frames)
		{
			if (!frame.HasContent)
			{
				continue;
			}

			maxBelow = Mathf.Max(
				maxBelow,
				Mathf.Max(0, frame.anchorBounds.yMin - frame.contentBounds.yMin)
			);
		}

		return maxBelow;
	}

	private int GetManualFrameYOffset(int frameIndex)
	{
		if (string.IsNullOrWhiteSpace(frameYOffsetOverrides))
		{
			return 0;
		}

		string[] entries = frameYOffsetOverrides.Split(',');

		foreach (string rawEntry in entries)
		{
			string entry = rawEntry.Trim();

			if (string.IsNullOrEmpty(entry))
			{
				continue;
			}

			string[] parts = entry.Split(':');

			if (parts.Length != 2)
			{
				continue;
			}

			if (
				TryParseFrameRange(parts[0].Trim(), out int startFrame, out int endFrame) &&
				frameIndex >= startFrame &&
				frameIndex <= endFrame &&
				int.TryParse(parts[1].Trim(), out int parsedYOffset)
			)
			{
				return parsedYOffset;
			}
		}

		return 0;
	}

	private bool ShouldFlipHorizontally(int frameIndex)
	{
		return IsFrameInList(horizontalFlipFrames, frameIndex);
	}

	private bool IsFrameInList(string frameList, int frameIndex)
	{
		if (string.IsNullOrWhiteSpace(frameList))
		{
			return false;
		}

		string[] entries = frameList.Split(',');

		foreach (string rawEntry in entries)
		{
			string entry = rawEntry.Trim();

			if (string.IsNullOrEmpty(entry))
			{
				continue;
			}

			if (
				TryParseFrameRange(entry, out int startFrame, out int endFrame) &&
				frameIndex >= startFrame &&
				frameIndex <= endFrame
			)
			{
				return true;
			}
		}

		return false;
	}

	private bool TryParseFrameRange(string text, out int startFrame, out int endFrame)
	{
		startFrame = 0;
		endFrame = 0;

		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		string[] rangeParts = text.Split('-');

		if (rangeParts.Length == 1)
		{
			if (!int.TryParse(rangeParts[0].Trim(), out startFrame))
			{
				return false;
			}

			endFrame = startFrame;
			return startFrame >= 0;
		}

		if (rangeParts.Length != 2)
		{
			return false;
		}

		if (
			!int.TryParse(rangeParts[0].Trim(), out startFrame) ||
			!int.TryParse(rangeParts[1].Trim(), out endFrame)
		)
		{
			return false;
		}

		if (startFrame < 0 || endFrame < 0)
		{
			return false;
		}

		if (startFrame > endFrame)
		{
			int oldStartFrame = startFrame;
			startFrame = endFrame;
			endFrame = oldStartFrame;
		}

		return true;
	}

	private void ConfigureOutputImporter(
		string outputAssetPath,
		int cellSize,
		int frameColumns,
		int frameRows
	)
	{
		TextureImporter importer = AssetImporter.GetAtPath(outputAssetPath) as TextureImporter;

		if (importer == null)
		{
			throw new InvalidOperationException("Output TextureImporter를 찾을 수 없습니다.");
		}

		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Multiple;
		importer.spritePixelsPerUnit = pixelsPerUnit;
		importer.mipmapEnabled = false;
		importer.filterMode = FilterMode.Point;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.alphaIsTransparency = true;
		importer.npotScale = TextureImporterNPOTScale.None;
		importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(frameColumns, frameRows) * cellSize);

		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		settings.spriteMeshType = SpriteMeshType.FullRect;
		importer.SetTextureSettings(settings);

		importer.SaveAndReimport();
	}

	private void ApplySpriteRects(
		string outputAssetPath,
		int cellSize,
		int frameColumns,
		int frameRows
	)
	{
		TextureImporter importer = AssetImporter.GetAtPath(outputAssetPath) as TextureImporter;

		if (importer == null)
		{
			throw new InvalidOperationException("Output TextureImporter를 찾을 수 없습니다.");
		}

		SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
		factory.Init();

		ISpriteEditorDataProvider dataProvider =
			factory.GetSpriteEditorDataProviderFromObject(importer);

		dataProvider.InitSpriteEditorDataProvider();

		List<SpriteRect> spriteRects = new List<SpriteRect>(frameColumns * frameRows);
		List<SpriteNameFileIdPair> nameFileIdPairs = new List<SpriteNameFileIdPair>(frameColumns * frameRows);

		string baseName = Path.GetFileNameWithoutExtension(outputAssetPath);

		for (int row = 0; row < frameRows; row++)
		{
			for (int column = 0; column < frameColumns; column++)
			{
				int index = row * frameColumns + column;

				GUID spriteId = GUID.Generate();

				SpriteRect spriteRect = new SpriteRect
				{
					name = $"{baseName}_{index:000}",
					spriteID = spriteId,
					rect = new Rect(
						column * cellSize,
						(frameRows - 1 - row) * cellSize,
						cellSize,
						cellSize
					),
					alignment = SpriteAlignment.Custom,
					pivot = spritePivot
				};

				spriteRects.Add(spriteRect);
				nameFileIdPairs.Add(new SpriteNameFileIdPair(spriteRect.name, spriteId));
			}
		}

		dataProvider.SetSpriteRects(spriteRects.ToArray());

		ISpriteNameFileIdDataProvider nameFileIdDataProvider =
			dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();

		if (nameFileIdDataProvider != null)
		{
			nameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
		}

		dataProvider.Apply();
		importer.SaveAndReimport();
	}

	private void CreateAnimationClip(string textureAssetPath)
	{
		AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);

		Sprite[] sprites = AssetDatabase
			.LoadAllAssetsAtPath(textureAssetPath)
			.OfType<Sprite>()
			.OrderBy(sprite => sprite.name, StringComparer.Ordinal)
			.ToArray();

		if (sprites.Length == 0)
		{
			Debug.LogWarning("Sprite sub-asset을 찾지 못해서 AnimationClip 생성을 건너뜁니다.");
			return;
		}

		float safeFrameRate = Mathf.Max(1.0f, frameRate);

		AnimationClip clip = new AnimationClip
		{
			frameRate = safeFrameRate,
			wrapMode = WrapMode.Once
		};

		EditorCurveBinding binding = new EditorCurveBinding
		{
			path = string.Empty,
			type = typeof(SpriteRenderer),
			propertyName = "m_Sprite"
		};

		ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Length + 1];

		for (int i = 0; i < sprites.Length; i++)
		{
			keyframes[i] = new ObjectReferenceKeyframe
			{
				time = i / safeFrameRate,
				value = sprites[i]
			};
		}

		keyframes[sprites.Length] = new ObjectReferenceKeyframe
		{
			time = sprites.Length / safeFrameRate,
			value = sprites[sprites.Length - 1]
		};

		AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

		AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopTime = false;
		AnimationUtility.SetAnimationClipSettings(clip, settings);

		string folder = Path.GetDirectoryName(textureAssetPath);
		string baseName = Path.GetFileNameWithoutExtension(textureAssetPath);
		string clipPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.anim");

		AssetDatabase.CreateAsset(clip, clipPath);
		AssetDatabase.SaveAssets();
	}
}

#endif
