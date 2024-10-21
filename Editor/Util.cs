using System.Collections.Generic;
using UnityEditor;

public class SessionStateConvenientListInt
{
    private readonly string m_Key;

    public SessionStateConvenientListInt(string key) => m_Key = key;
    public int[] Get() => SessionState.GetIntArray(m_Key, new int[] { });
    public void Clear() => SessionState.EraseIntArray(m_Key);
    public int Count() => Get().Length;

    public void Queue(int value) => SessionState.SetIntArray(m_Key, new List<int>(SessionState.GetIntArray(m_Key, new int[] { })) { value }.ToArray());// @value add
    public int Dequeue()
    {
        int[] array = Get();
        Clear();
        for (int i = 1; i < array.Length - 1; i++) Queue(array[i]);
        return array[0];
    }
}