using System.Collections.Generic;
using UnityEngine;

public static class ExploreMapLayout
{
	const float LeftLaneX = 0.20f;
	const float CenterLaneX = 0.50f;
	const float RightLaneX = 0.80f;
	const float MinNodeY = 0.07f;
	const float MaxNodeY = 0.93f;
	const float DefaultViewportWidth = 596f;
	const float DefaultViewportHeight = 762f;
	const float DefaultNodePadding = 20f;
	const float DefaultSideMargin = 48f;
	const float DefaultTopMargin = 56f;
	const float DefaultBottomMargin = 72f;
	const float DefaultCurrentScrollTargetFromBottom = 0.32f;
	const int MaxOverlapResolutionAttempts = 6;

	public static Vector2 ResolveNodeCenter(ExploreMapNodeView node)
	{
		if (node.NormalizedPosition != Vector2.zero)
			return node.NormalizedPosition;
		return ResolveDefaultNodeCenter(node.Row, node.Lane);
	}

	public static Vector2 ResolveDefaultNodeCenter(int row, int lane)
	{
		float y = Mathf.Lerp(MinNodeY, MaxNodeY, row / (float)ExploreMapPresentationPolicy.BossRow);
		float x;
		if (row == ExploreMapPresentationPolicy.StartRow || row == ExploreMapPresentationPolicy.BossRow)
			x = CenterLaneX;
		else
			x = lane == 0 ? LeftLaneX : lane == 1 ? CenterLaneX : RightLaneX;
		return new Vector2(x, y);
	}

	public static Vector2 ResolveNodeSize(ExploreMapNodeKind kind)
	{
		switch (kind)
		{
			case ExploreMapNodeKind.Boss:
				return new Vector2(146f, 128f);
			case ExploreMapNodeKind.Start:
				return new Vector2(134f, 118f);
			default:
				return new Vector2(126f, 112f);
		}
	}

	public static ExploreMapLayoutConfig CreateDefaultConfig(Vector2 viewportSize)
	{
		float viewportWidth = viewportSize.x > 1f ? viewportSize.x : DefaultViewportWidth;
		float viewportHeight = viewportSize.y > 1f ? viewportSize.y : DefaultViewportHeight;
		float normalDiameter = ResolveMaxNodeDiameter(false);
		float currentDiameter = ResolveMaxNodeDiameter(true);
		return new ExploreMapLayoutConfig(
			viewportWidth,
			viewportHeight,
			normalDiameter,
			currentDiameter,
			Mathf.Max(normalDiameter + DefaultNodePadding, 156f),
			Mathf.Max(currentDiameter + 48f, 176f),
			112f,
			DefaultSideMargin,
			DefaultTopMargin,
			DefaultBottomMargin,
			DefaultNodePadding,
			DefaultCurrentScrollTargetFromBottom);
	}

	public static ExploreMapLayoutResult Build(ExploreMapPresentation presentation, ExploreMapLayoutConfig config)
	{
		var resolvedConfig = ResolveConfig(config);
		var result = BuildResolved(presentation, resolvedConfig);
		int attempts = 0;
		while (result.HasOverlaps && attempts < MaxOverlapResolutionAttempts)
		{
			resolvedConfig = resolvedConfig.WithLayerSpacing(resolvedConfig.MinLayerSpacingY + 24f);
			result = BuildResolved(presentation, resolvedConfig);
			attempts++;
		}
		return result;
	}

	static ExploreMapLayoutConfig ResolveConfig(ExploreMapLayoutConfig config)
	{
		float viewportWidth = config.ViewportWidth > 1f ? config.ViewportWidth : DefaultViewportWidth;
		float viewportHeight = config.ViewportHeight > 1f ? config.ViewportHeight : DefaultViewportHeight;
		float normalDiameter = Mathf.Max(config.NodeDiameterNormal, ResolveMaxNodeDiameter(false));
		float currentDiameter = Mathf.Max(config.NodeDiameterCurrent, ResolveMaxNodeDiameter(true));
		float nodePadding = Mathf.Max(0f, config.NodePadding);
		float minNodeSeparation = Mathf.Max(config.MinNodeSeparation, normalDiameter + nodePadding);
		float minLayerSpacingY = Mathf.Max(config.MinLayerSpacingY, currentDiameter + nodePadding);
		float sideMargin = Mathf.Max(0f, config.SideMargin);
		float topMargin = Mathf.Max(0f, config.TopMargin);
		float bottomMargin = Mathf.Max(0f, config.BottomMargin);
		float scrollTarget = Mathf.Clamp01(
			config.CurrentScrollTargetFromBottom > 0f
				? config.CurrentScrollTargetFromBottom
				: DefaultCurrentScrollTargetFromBottom);
		return new ExploreMapLayoutConfig(
			viewportWidth,
			viewportHeight,
			normalDiameter,
			currentDiameter,
			minNodeSeparation,
			minLayerSpacingY,
			Mathf.Max(0f, config.StageGapY),
			sideMargin,
			topMargin,
			bottomMargin,
			nodePadding,
			scrollTarget);
	}

	static ExploreMapLayoutResult BuildResolved(ExploreMapPresentation presentation, ExploreMapLayoutConfig config)
	{
		int nodeCount = presentation.NodeCount;
		if (nodeCount <= 0)
			return new ExploreMapLayoutResult(
				new ExploreMapNodeLayout[0],
				config.ViewportWidth,
				config.ViewportHeight,
				false,
				0,
				config);

		var nodeLayouts = new ExploreMapNodeLayout[nodeCount];
		var rows = BuildRows(presentation);
		float y = config.BottomMargin;
		bool hasAnySubrow = false;

		for (int row = ExploreMapPresentationPolicy.StartRow; row <= ExploreMapPresentationPolicy.BossRow; row++)
		{
			if (!rows.TryGetValue(row, out var nodeIndices) || nodeIndices.Count == 0)
				continue;

			nodeIndices.Sort((a, b) => CompareNodes(presentation.GetNode(a), presentation.GetNode(b), a, b));
			int maxNodesPerSubrow = ResolveMaxNodesPerSubrow(config);
			int subrowCount = Mathf.CeilToInt(nodeIndices.Count / (float)maxNodesPerSubrow);
			for (int subrow = 0; subrow < subrowCount; subrow++)
			{
				if (hasAnySubrow)
					y += config.MinLayerSpacingY;

				int startIndex = subrow * maxNodesPerSubrow;
				int count = Mathf.Min(maxNodesPerSubrow, nodeIndices.Count - startIndex);
				PlaceSubrow(presentation, config, nodeIndices, startIndex, count, y, nodeLayouts);
				hasAnySubrow = true;
			}
		}

		float contentHeight = Mathf.Max(config.ViewportHeight, y + config.TopMargin);
		int overlapCount = CountOverlaps(nodeLayouts);
		return new ExploreMapLayoutResult(
			nodeLayouts,
			config.ViewportWidth,
			contentHeight,
			overlapCount > 0,
			overlapCount,
			config);
	}

	static Dictionary<int, List<int>> BuildRows(ExploreMapPresentation presentation)
	{
		var rows = new Dictionary<int, List<int>>();
		for (int i = 0; i < presentation.NodeCount; i++)
		{
			var node = presentation.GetNode(i);
			if (!rows.TryGetValue(node.Row, out var indices))
			{
				indices = new List<int>();
				rows.Add(node.Row, indices);
			}
			indices.Add(i);
		}
		return rows;
	}

	static int CompareNodes(ExploreMapNodeView a, ExploreMapNodeView b, int aIndex, int bIndex)
	{
		int laneCompare = a.Lane.CompareTo(b.Lane);
		if (laneCompare != 0)
			return laneCompare;

		int idCompare = string.CompareOrdinal(a.NodeId, b.NodeId);
		if (idCompare != 0)
			return idCompare;

		return aIndex.CompareTo(bIndex);
	}

	static int ResolveMaxNodesPerSubrow(ExploreMapLayoutConfig config)
	{
		float usableWidth = ResolveUsableWidth(config);
		if (usableWidth <= 0f)
			return 1;

		return Mathf.Max(1, Mathf.FloorToInt(usableWidth / config.MinNodeSeparation) + 1);
	}

	static float ResolveUsableWidth(ExploreMapLayoutConfig config)
	{
		return Mathf.Max(0f, config.ViewportWidth - config.SideMargin * 2f);
	}

	static void PlaceSubrow(
		ExploreMapPresentation presentation,
		ExploreMapLayoutConfig config,
		List<int> nodeIndices,
		int startIndex,
		int count,
		float y,
		ExploreMapNodeLayout[] nodeLayouts)
	{
		float usableWidth = ResolveUsableWidth(config);
		for (int i = 0; i < count; i++)
		{
			int nodeIndex = nodeIndices[startIndex + i];
			var node = presentation.GetNode(nodeIndex);
			float x = ResolveNodeX(config, usableWidth, i, count);
			float diameter = ResolveCollisionDiameter(node, config);
			Vector2 center = new Vector2(x, y);
			nodeLayouts[nodeIndex] = new ExploreMapNodeLayout(
				nodeIndex,
				node.NodeId,
				center,
				ExploreMapLayout.ResolveNodeSize(node.Kind),
				diameter);
		}
	}

	static float ResolveNodeX(ExploreMapLayoutConfig config, float usableWidth, int index, int count)
	{
		if (count <= 1 || usableWidth <= 0f)
			return config.ViewportWidth * 0.5f;

		float t = index / (float)(count - 1);
		return config.SideMargin + usableWidth * t;
	}

	static float ResolveCollisionDiameter(ExploreMapNodeView node, ExploreMapLayoutConfig config)
	{
		Vector2 size = ResolveNodeSize(node.Kind);
		float visualDiameter = Mathf.Max(size.x, size.y);
		if (node.IsCurrent)
			visualDiameter = Mathf.Max(visualDiameter, config.NodeDiameterCurrent);
		return visualDiameter + config.NodePadding;
	}

	static int CountOverlaps(ExploreMapNodeLayout[] nodeLayouts)
	{
		if (nodeLayouts == null || nodeLayouts.Length <= 1)
			return 0;

		int count = 0;
		for (int i = 0; i < nodeLayouts.Length; i++)
		{
			if (!nodeLayouts[i].IsValid)
				continue;

			Rect a = nodeLayouts[i].CollisionRect;
			for (int j = i + 1; j < nodeLayouts.Length; j++)
			{
				if (!nodeLayouts[j].IsValid)
					continue;
				if (a.Overlaps(nodeLayouts[j].CollisionRect))
					count++;
			}
		}
		return count;
	}

	static float ResolveMaxNodeDiameter(bool includeCurrent)
	{
		float diameter = Mathf.Max(ResolveNodeSize(ExploreMapNodeKind.Combat).x, ResolveNodeSize(ExploreMapNodeKind.Combat).y);
		Vector2 start = ResolveNodeSize(ExploreMapNodeKind.Start);
		Vector2 boss = ResolveNodeSize(ExploreMapNodeKind.Boss);
		diameter = Mathf.Max(diameter, start.x);
		diameter = Mathf.Max(diameter, start.y);
		diameter = Mathf.Max(diameter, boss.x);
		diameter = Mathf.Max(diameter, boss.y);
		if (includeCurrent)
			diameter = Mathf.Max(diameter, 146f);
		return diameter;
	}
}

public readonly struct ExploreMapLayoutConfig
{
	public readonly float ViewportWidth;
	public readonly float ViewportHeight;
	public readonly float NodeDiameterNormal;
	public readonly float NodeDiameterCurrent;
	public readonly float MinNodeSeparation;
	public readonly float MinLayerSpacingY;
	public readonly float StageGapY;
	public readonly float SideMargin;
	public readonly float TopMargin;
	public readonly float BottomMargin;
	public readonly float NodePadding;
	public readonly float CurrentScrollTargetFromBottom;

	public ExploreMapLayoutConfig(
		float viewportWidth,
		float viewportHeight,
		float nodeDiameterNormal,
		float nodeDiameterCurrent,
		float minNodeSeparation,
		float minLayerSpacingY,
		float stageGapY,
		float sideMargin,
		float topMargin,
		float bottomMargin,
		float nodePadding,
		float currentScrollTargetFromBottom)
	{
		ViewportWidth = viewportWidth;
		ViewportHeight = viewportHeight;
		NodeDiameterNormal = nodeDiameterNormal;
		NodeDiameterCurrent = nodeDiameterCurrent;
		MinNodeSeparation = minNodeSeparation;
		MinLayerSpacingY = minLayerSpacingY;
		StageGapY = stageGapY;
		SideMargin = sideMargin;
		TopMargin = topMargin;
		BottomMargin = bottomMargin;
		NodePadding = nodePadding;
		CurrentScrollTargetFromBottom = currentScrollTargetFromBottom;
	}

	public ExploreMapLayoutConfig WithLayerSpacing(float minLayerSpacingY)
	{
		return new ExploreMapLayoutConfig(
			ViewportWidth,
			ViewportHeight,
			NodeDiameterNormal,
			NodeDiameterCurrent,
			MinNodeSeparation,
			minLayerSpacingY,
			StageGapY,
			SideMargin,
			TopMargin,
			BottomMargin,
			NodePadding,
			CurrentScrollTargetFromBottom);
	}
}

public readonly struct ExploreMapNodeLayout
{
	public readonly int NodeIndex;
	public readonly string NodeId;
	public readonly Vector2 Center;
	public readonly Vector2 Size;
	public readonly float CollisionDiameter;

	public bool IsValid => NodeIndex >= 0 && !string.IsNullOrEmpty(NodeId);

	public Rect CollisionRect => new Rect(
		Center.x - CollisionDiameter * 0.5f,
		Center.y - CollisionDiameter * 0.5f,
		CollisionDiameter,
		CollisionDiameter);

	public ExploreMapNodeLayout(
		int nodeIndex,
		string nodeId,
		Vector2 center,
		Vector2 size,
		float collisionDiameter)
	{
		NodeIndex = nodeIndex;
		NodeId = nodeId;
		Center = center;
		Size = size;
		CollisionDiameter = collisionDiameter;
	}
}

public readonly struct ExploreMapLayoutResult
{
	readonly ExploreMapNodeLayout[] nodes;
	readonly ExploreMapLayoutConfig config;

	public readonly float ContentWidth;
	public readonly float ContentHeight;
	public readonly bool HasOverlaps;
	public readonly int OverlapCount;

	public int NodeCount => nodes != null ? nodes.Length : 0;
	public ExploreMapLayoutConfig Config => config;

	public ExploreMapLayoutResult(
		ExploreMapNodeLayout[] nodes,
		float contentWidth,
		float contentHeight,
		bool hasOverlaps,
		int overlapCount,
		ExploreMapLayoutConfig config)
	{
		this.nodes = nodes;
		ContentWidth = contentWidth;
		ContentHeight = contentHeight;
		HasOverlaps = hasOverlaps;
		OverlapCount = overlapCount;
		this.config = config;
	}

	public ExploreMapNodeLayout GetNode(int index)
	{
		if (nodes == null || index < 0 || index >= nodes.Length)
			return default;
		return nodes[index];
	}

	public bool TryGetNode(int index, out ExploreMapNodeLayout node)
	{
		node = GetNode(index);
		return node.IsValid;
	}

	public int FindNodeIndex(string nodeId)
	{
		if (nodes == null || string.IsNullOrEmpty(nodeId))
			return -1;

		for (int i = 0; i < nodes.Length; i++)
			if (nodes[i].NodeId == nodeId)
				return i;
		return -1;
	}

	public float CalculateVerticalNormalizedPosition(int nodeIndex, float viewportHeight)
	{
		return CalculateVerticalNormalizedPosition(
			nodeIndex,
			viewportHeight,
			config.CurrentScrollTargetFromBottom);
	}

	public float CalculateVerticalNormalizedPosition(
		int nodeIndex,
		float viewportHeight,
		float targetFromBottomRatio)
	{
		if (!TryGetNode(nodeIndex, out var node))
			return 0f;

		float visibleHeight = viewportHeight > 1f ? viewportHeight : config.ViewportHeight;
		float scrollableHeight = Mathf.Max(0f, ContentHeight - visibleHeight);
		if (scrollableHeight <= 0.001f)
			return 0f;

		float targetRatio = Mathf.Clamp01(targetFromBottomRatio);
		float visibleBottom = Mathf.Clamp(
			node.Center.y - visibleHeight * targetRatio,
			0f,
			scrollableHeight);
		return Mathf.Clamp01(visibleBottom / scrollableHeight);
	}
}
