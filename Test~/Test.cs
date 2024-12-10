using UnityEngine;

public class Test : MonoBehaviour
{
    private float m_Timer;
    protected void Start()
    {
        if (Editors.IsAdditional()) {
            Debug.Log("Additional said something like intended");
        }else{
            Debug.Log("Original said something like intended");
        }
#if GLOBAL_SCRIPTING_DEFINE
        Debug.Log("Respecting the Global scripting define");
#endif
#if ADDITIONAL_ONLY_SCRIPTING_DEFINE
        Debug.Log("Additional said something else like intended");
#else
        Debug.Log("Original said something else like intended");
#endif
        m_Timer = 20;
    }

    protected void Update()
    {
        if (m_Timer > 0) m_Timer -= Time.deltaTime;

        if (m_Timer <= 0) {
#if UNITY_EDITOR
            Editors.TestComplete();
#endif
        }
    }

    [AfterTest]
    public static void MethodToCallAfterPlayMode()
    {
        Debug.Log("This method is called after exiting Play Mode when 'Start Tests' is used");
    }
}
