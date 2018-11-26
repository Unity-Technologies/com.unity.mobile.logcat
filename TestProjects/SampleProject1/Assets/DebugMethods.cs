using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugMethods : MonoBehaviour
{

  // Start is called before the first frame update
  void Start()
  {
    StartCoroutine("runner");
  }


  IEnumerator runner()
  {
    while (true)
    {
      TestDebug();
      yield return new WaitForSeconds(2);

    }
  }

  public void TestDebug()
  {
    Debug.Log("This is a simple LOG message");
    Debug.Log("This is a simple LOG message, with context", this.gameObject);
    // Debug.Assert(true);
    // Debug.Assert(true, this.gameObject);
    // Debug.Assert(true, "A simple Assert true");
    // Debug.Assert(false);
    // Debug.Assert(false, this.gameObject);
    // Debug.Assert(false, "A simple Assert false");
    Debug.LogAssertion("This is a simple ASSERTION message");
    Debug.LogAssertion("This is a simple ASSERTION message, with context", this.gameObject);
    Debug.LogError("This is a simple ERROR message");
    Debug.LogError("This is a simple ERROR message, with context", this.gameObject);
    Debug.LogException(new UnityException("This is a simple EXCEPTION message"));
    Debug.LogException(new UnityException("This is a simple EXCEPTION message, with context"), this.gameObject);
    Debug.LogWarning("This is a simple WARNING message");
    Debug.LogWarning("This is a simple WARNING message, with context", this.gameObject);
  }
}
