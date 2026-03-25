// Assets/Scripts/Dice/DiceResultDisplay.cs
// DiceRoller.onRollComplete 이벤트를 구독해 결과 숫자를 UI 텍스트로 표시한다.

using TMPro;
using UnityEngine;

public class DiceResultDisplay : MonoBehaviour
{
	[SerializeField] private TMP_Text  resultText;
	[SerializeField] private DiceRoller diceRoller;

	private void Start()
	{
		if (diceRoller != null)
			diceRoller.onRollComplete.AddListener(ShowResult);
	}

	private void OnDestroy()
	{
		if (diceRoller != null)
			diceRoller.onRollComplete.RemoveListener(ShowResult);
	}

	public void ShowResult(int value)
	{
		if (resultText != null)
			resultText.text = value.ToString();
	}
}
