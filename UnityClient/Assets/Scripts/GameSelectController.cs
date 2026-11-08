using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
 * 게임 선택 화면의 버튼을 연결한다.
 *
 * Unity 설정:
 * 1. GameSelect 씬에 빈 오브젝트 GameSelectController를 만든다.
 * 2. 이 스크립트를 그 오브젝트에 추가한다.
 * 3. Inspector에 Grip Exercise 버튼과 Back 버튼을 연결한다.
 * 4. 세 씬을 Build Profiles의 Scene List에 추가한다.
 */
public class GameSelectController : MonoBehaviour
{
    [SerializeField] private Button gripExerciseButton;
    [SerializeField] private Button backButton;

    private void Start()
    {
        if (gripExerciseButton == null || backButton == null)
        {
            Debug.LogError("GameSelectController: 버튼 연결을 확인하세요.");
            return;
        }

        gripExerciseButton.onClick.AddListener(OpenGripExercise);
        backButton.onClick.AddListener(GoBack);
    }

    private void OnDestroy()
    {
        if (gripExerciseButton != null)
            gripExerciseButton.onClick.RemoveListener(OpenGripExercise);

        if (backButton != null)
            backButton.onClick.RemoveListener(GoBack);
    }

    private void OpenGripExercise()
    {
        SceneManager.LoadScene("RehabGameUI");
    }

    private void GoBack()
    {
        SceneManager.LoadScene("MainMenu");
    }
}