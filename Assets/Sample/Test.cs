using Dou.GPUSkinning;
using UnityEngine;

public class Test : MonoBehaviour
{
    public Transform parent;
    
    public GameObject prefab;
    
    private string[] _animNames = new[]
    {
        "Walk",
        "Run",
        "Eat",
        "Jump",
        "Idle",
        "Rest",
        "Attack",
        "Damage",
        "Die",
    };
    
    void Start()
    {
        for (int i = 0; i < 100; i++)
        {
            var go = Instantiate(prefab, parent);
            go.transform.position = new Vector3(((i / 10f) - 5) * 0.5f, 0, ((i % 10f) - 5) * 0.5f);
            var anim = go.GetComponent<GPUSkinningAnimation>();
            anim.Play(_animNames[Random.Range(0, _animNames.Length - 1)]);
        }
    }
}
