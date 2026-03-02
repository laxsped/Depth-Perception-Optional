using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WSRestrictionTrigger : MonoBehaviour
{
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        SetRestriction(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        SetRestriction(other, false);
    }

    private static void SetRestriction(Collider other, bool isLocked)
    {
        PlayerWASDAnimator controller = other.GetComponent<PlayerWASDAnimator>();
        if (controller == null)
        {
            controller = other.GetComponentInParent<PlayerWASDAnimator>();
        }

        if (controller != null)
        {
            controller.SetVerticalInputLocked(isLocked);
        }

        GrassFootstepNoise grassSteps = other.GetComponent<GrassFootstepNoise>();
        if (grassSteps == null)
        {
            grassSteps = other.GetComponentInParent<GrassFootstepNoise>();
        }

        if (grassSteps != null)
        {
            grassSteps.SetVerticalInputLocked(isLocked);
        }
    }
}
