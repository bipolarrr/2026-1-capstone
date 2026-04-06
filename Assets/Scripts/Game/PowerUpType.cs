public enum PowerUpType
{
	/// <summary>홀수 눈만 또는 짝수 눈만으로 점수를 냈다면 데미지 2배</summary>
	OddEvenDouble,

	/// <summary>모든 족보 데미지 절반, 족보가 아니면 데미지 2배</summary>
	AllOrNothing,

	/// <summary>일회성 부활: 사망에 이르는 데미지 1회 무효화 후 소멸</summary>
	ReviveOnce
}
