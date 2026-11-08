using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
 * MainMenu 시작 버튼을 재활 게임 화면에 연결한다.
 *
 * 실행 방법:
 * 1. MainMenu 씬의 빈 오브젝트에 이 스크립트를 추가한다.
 * 2. Inspector의 Start Button 칸에 StartButton을 지정한다.
 * 3. MainMenu와 RehabGameUI를 Build Profiles의 Scene List에 넣는다.
 * 4. Play에서 Start Exercise를 누른다.
 */
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private void Start()
    {
        if (startButton == null)
        {
            Debug.LogError("MainMenuController: Start Button을 연결하세요.");
            return;
        }

        startButton.onClick.AddListener(StartExercise);
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartExercise);
    }

    private void StartExercise()
    {
        SceneManager.LoadScene("GameSelect");
    }
}