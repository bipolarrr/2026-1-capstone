using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 안의 한 라운드 유형.
///   NormalCombat: 일반 몹 조우 — StageData.mobPool에서 뽑아 전투
///   ItemBox:     아이템/파워업 드랍 세션
///   BossCombat:  스테이지 보스 (stage.boss 사용)
/// </summary>
public enum StageRoundType
{
	NormalCombat,
	ItemBox,
	BossCombat,
}

/// <summary>
/// 스테이지의 몹 풀에 들어가는 일반 몹 정의.
/// spritePath가 비어 있거나 파일이 없으면 themeColor로 폴백 플레이스홀더 스프라이트를 자동 생성한다.
/// </summary>
public class MobDef
{
	public string name;
	public int    hpMin;
	public int    hpMax;
	public int    rank;
	public Color  themeColor;         // 스프라이트 로딩 실패 시 폴백 색상
	public string spritePath;         // null 또는 파일 없음 → themeColor 폴백
	public string idleSpriteFolderPath;
	public string attackSpriteFolderPath;
	public string hitSpriteFolderPath;
	public float  bodyYMin;           // 적 슬롯 내 Y 앵커 (0 = 바닥)
	public float  bodyYMax;
	// 몹 바디의 가로 앵커. 기본 0.05~0.95. 슬롯을 넘는 값(예: -0.1~1.1)을 허용해
	// preserveAspect 이미지를 실제로 더 크게 보이게 할 수 있다 (예: 거대 골렘).
	public float  bodyXMin          = 0.05f;
	public float  bodyXMax          = 0.95f;
	// 몹별 UI 튜닝 — 비우면 기본값 사용. 모든 무기 배틀 씬 공통.
	public float  borderThickness = 0.05f; // 강조 테두리 두께 (슬롯 비율)
	public float  infoPanelGap    = 0.18f; // 바디 머리~이름/HP 패널 간격 (슬롯 비율)
}

/// <summary>스테이지 보스 정의.</summary>
public class BossDef
{
	public string name;
	public int    hp;
	public int    rank;
	public Color  themeColor;
	public string spritePath;
}

/// <summary>
/// 한 스테이지의 전체 컨셉. 새 스테이지를 만들려면
/// Assets/Scripts/Stages/ 아래에 static Build() 메서드를 가진 파일을 만들고
/// StageRegistry에 Register하면 된다.
/// </summary>
public class StageData
{
	public string              id;                       // "forest_1" 같은 고유 식별자
	public string              displayName;              // UI 표기용 이름
	public Color               themeColor;               // 배경 폴백 색
	public string              backgroundSpritePath;     // null 또는 파일 없음 → themeColor 폴백
	public List<MobDef>        mobPool;                  // 이 스테이지의 일반 몹 후보
	public List<StageRoundType> rounds;                  // 스테이지 내 라운드 시퀀스
	public BossDef             boss;                     // BossCombat 라운드에서 등장
	public int                 normalEnemyCountMin = 2;
	public int                 normalEnemyCountMax = 4;  // 배타적 상한 (Random.Range)

	// 플레이어 바디 Y 앵커에 더해질 델타 (0 = 기본 GroundY).
	// 전투 씬과 Explore 씬의 playerBody.anchorMin/Max.y에 동일하게 가산된다.
	public float               playerGroundYOffset = 0f;

	public MobDef FindMob(string mobName)
	{
		if (mobPool == null) return null;
		for (int i = 0; i < mobPool.Count; i++)
			if (mobPool[i].name == mobName) return mobPool[i];
		return null;
	}

	public int IndexOfMob(string mobName)
	{
		if (mobPool == null) return -1;
		for (int i = 0; i < mobPool.Count; i++)
			if (mobPool[i].name == mobName) return i;
		return -1;
	}
}
