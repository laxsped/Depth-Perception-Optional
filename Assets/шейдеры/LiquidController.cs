using UnityEngine;

public class LiquidController : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private Rigidbody bottleRigidbody;
    [SerializeField] private Renderer liquidRenderer;

    [Header("Fill")]
    [Range(0f, 1f)]
    public float fillAmount = 0.55f;

    [Header("Local Y Range (liquid mesh)")]
    [SerializeField] private float minY = -0.5f;
    [SerializeField] private float maxY = 0.5f;

    [Header("Responsive Slosh")]
    [SerializeField] private bool autoWobble = true;
    [SerializeField] [Range(0f, 6f)] private float moveResponse = 2.4f;
    [SerializeField] [Range(0f, 6f)] private float angularResponse = 2.0f;
    [SerializeField] [Range(0f, 60f)] private float spring = 22f;
    [SerializeField] [Range(0f, 60f)] private float damping = 10f;
    [SerializeField] [Range(0.01f, 1f)] private float wobbleClamp = 0.30f;
    [SerializeField] [Range(0f, 3f)] private float transformVelocityWeight = 1.0f;
    [SerializeField] [Range(0.5f, 12f)] private float shaderWobbleScale = 6f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] [Range(0.05f, 1f)] private float debugInterval = 0.2f;

    private Material mat;
    private Transform bodyTf;

    private Vector3 lastBodyPos;
    private Quaternion lastBodyRot;

    private float wobbleX;
    private float wobbleZ;
    private float velX;
    private float velZ;
    private float nextDebugTime;

    private static readonly int PFill = Shader.PropertyToID("_FillAmount");
    private static readonly int PWobbleX = Shader.PropertyToID("_WobbleX");
    private static readonly int PWobbleZ = Shader.PropertyToID("_WobbleZ");
    private static readonly int PMinY = Shader.PropertyToID("_MinY");
    private static readonly int PMaxY = Shader.PropertyToID("_MaxY");
    private static readonly int PSloshScale = Shader.PropertyToID("_SloshScale");
    private static readonly int PFillLevelWS = Shader.PropertyToID("_FillLevelWS");
    private static readonly int PPivotWS = Shader.PropertyToID("_PivotWS");
    private static readonly int PDepthSpanWS = Shader.PropertyToID("_DepthSpanWS");

    private void Awake()
    {
        if (bottleRigidbody == null)
        {
            bottleRigidbody = GetComponentInParent<Rigidbody>();
        }

        if (liquidRenderer == null)
        {
            liquidRenderer = GetComponent<Renderer>();
            if (liquidRenderer == null)
            {
                liquidRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (liquidRenderer != null)
        {
            mat = liquidRenderer.material;

            MeshFilter mf = liquidRenderer.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                minY = b.min.y;
                maxY = b.max.y;
            }
        }

        bodyTf = bottleRigidbody != null ? bottleRigidbody.transform : transform;
        lastBodyPos = bodyTf.position;
        lastBodyRot = bodyTf.rotation;

        ApplyToShader();

        if (debugLogs)
        {
            if (mat == null)
            {
                Debug.LogWarning("[LiquidController] Material not found on liquidRenderer.", this);
            }
            else
            {
                string shaderName = mat.shader != null ? mat.shader.name : "<null>";
                Debug.Log("[LiquidController] init: renderer=" + liquidRenderer.name + ", shader=" + shaderName +
                          ", minY=" + minY.ToString("F3") + ", maxY=" + maxY.ToString("F3"), this);
            }
        }
    }

    private void OnValidate()
    {
        fillAmount = Mathf.Clamp01(fillAmount);
        wobbleClamp = Mathf.Clamp(wobbleClamp, 0.01f, 1f);
        if (maxY < minY)
        {
            float t = minY;
            minY = maxY;
            maxY = t;
        }
    }

    private void Update()
    {
        if (mat == null)
        {
            return;
        }

        if (autoWobble)
        {
            SimulateSlosh();
        }
        else
        {
            wobbleX = 0f;
            wobbleZ = 0f;
            velX = 0f;
            velZ = 0f;
        }

        ApplyToShader();
    }

    private void SimulateSlosh()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 trVelWS = (bodyTf.position - lastBodyPos) / dt;

        Quaternion dRot = bodyTf.rotation * Quaternion.Inverse(lastBodyRot);
        dRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsNaN(axis.x))
        {
            axis = Vector3.zero;
            angleDeg = 0f;
        }
        Vector3 trAngVelWS = axis.normalized * (Mathf.Deg2Rad * angleDeg / dt);

        Vector3 rbVelWS = Vector3.zero;
        Vector3 rbAngVelWS = Vector3.zero;
        if (bottleRigidbody != null)
        {
            rbVelWS = bottleRigidbody.linearVelocity;
            rbAngVelWS = bottleRigidbody.angularVelocity;
        }

        Vector3 usedVelWS = rbVelWS.sqrMagnitude > trVelWS.sqrMagnitude ? rbVelWS : trVelWS * transformVelocityWeight;
        Vector3 usedAngWS = rbAngVelWS.sqrMagnitude > trAngVelWS.sqrMagnitude ? rbAngVelWS : trAngVelWS;

        Vector3 localVel = bodyTf.InverseTransformDirection(usedVelWS);
        Vector3 localAng = bodyTf.InverseTransformDirection(usedAngWS);

        float targetX = Mathf.Clamp((-localVel.x * 0.08f * moveResponse) + (-localAng.z * 0.22f * angularResponse), -wobbleClamp, wobbleClamp);
        float targetZ = Mathf.Clamp((-localVel.z * 0.08f * moveResponse) + ( localAng.x * 0.22f * angularResponse), -wobbleClamp, wobbleClamp);

        velX += (targetX - wobbleX) * spring * dt;
        velZ += (targetZ - wobbleZ) * spring * dt;

        velX *= Mathf.Clamp01(1f - damping * dt);
        velZ *= Mathf.Clamp01(1f - damping * dt);

        wobbleX += velX * dt;
        wobbleZ += velZ * dt;

        wobbleX = Mathf.Clamp(wobbleX, -wobbleClamp, wobbleClamp);
        wobbleZ = Mathf.Clamp(wobbleZ, -wobbleClamp, wobbleClamp);

        lastBodyPos = bodyTf.position;
        lastBodyRot = bodyTf.rotation;

        if (debugLogs && Time.unscaledTime >= nextDebugTime)
        {
            nextDebugTime = Time.unscaledTime + Mathf.Max(0.05f, debugInterval);
            Debug.Log("[LiquidController] speed=" + usedVelWS.magnitude.ToString("F2") +
                      " ang=" + usedAngWS.magnitude.ToString("F2") +
                      " target=(" + targetX.ToString("F3") + "," + targetZ.ToString("F3") + ")" +
                      " wobble=(" + wobbleX.ToString("F3") + "," + wobbleZ.ToString("F3") + ")" +
                      " fill=" + fillAmount.ToString("F2"), this);
        }
    }

    private void ApplyToShader()
    {
        mat.SetFloat(PFill, fillAmount);
        mat.SetFloat(PWobbleX, wobbleX);
        mat.SetFloat(PWobbleZ, wobbleZ);
        mat.SetFloat(PMinY, minY);
        mat.SetFloat(PMaxY, maxY);
        mat.SetFloat(PSloshScale, shaderWobbleScale);

        if (liquidRenderer != null)
        {
            Transform lt = liquidRenderer.transform;
            Bounds bw = liquidRenderer.bounds;
            float fillY = Mathf.Lerp(bw.min.y, bw.max.y, Mathf.Clamp01(fillAmount));

            mat.SetFloat(PFillLevelWS, fillY);
            mat.SetVector(PPivotWS, bw.center);
            mat.SetFloat(PDepthSpanWS, Mathf.Max(0.0001f, bw.size.y));
        }
    }
}



