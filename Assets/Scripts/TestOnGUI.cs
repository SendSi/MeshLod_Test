//using UnityEditor;
using Unity.Profiling;
using UnityEngine;

public class TestOnGUI : MonoBehaviour
{
    //private ProfilerAPI ProfilerAPI;
    //private void Start()
    //{
    //    ProfilerAPI = this.GetComponent<ProfilerAPI>();
    //}

    private void OnGUI()
    {

        //GUILayout.TextField("Total DrawCall: " + UnityStats.drawCalls);
        //GUILayout.TextField("Batch: " + UnityStats.batches);
        //GUILayout.TextField("Static Batch DC: " + UnityStats.staticBatchedDrawCalls);
        //GUILayout.TextField("Static Batch: " + UnityStats.staticBatches);
        //GUILayout.TextField("DynamicBatch DC: " + UnityStats.dynamicBatchedDrawCalls);
        //GUILayout.TextField("DynamicBatch: " + UnityStats.dynamicBatches);
        //TrianglesCount = 3,
        //VerticesCount = 4,
        GUILayout.Label("面数: " + GetRenderNumber(3));
        GUILayout.Label("顶点: " + GetRenderNumber(4));
        GUILayout.Space(10);
        if (GUILayout.Button("<size=60>  远  </size>"))
        {
            this.transform.Translate(Vector3.back*2);
        }
        if (GUILayout.Button("<size=60>  近  </size>"))
        {
            this.transform.Translate(Vector3.forward*2);
        }
    }


    ProfilerRecorder SetPassCallsCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
    ProfilerRecorder TotalBatchesCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
    ProfilerRecorder DrawCallsCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
    ProfilerRecorder TrianglesCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
    ProfilerRecorder VerticesCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
    ProfilerRecorder ShadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
    ProfilerRecorder VisibleSkinnedMeshes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Visible Skinned Meshes Count");
    ProfilerRecorder DynamicBatchCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Dynamic Batched Draw Calls Count");
    ProfilerRecorder BatchCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, " Batched Draw Calls Count");
    ProfilerRecorder InstancedBatchCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Instanced Batched Draw Calls Count");
    ProfilerRecorder RenderTexturesBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes");
    ProfilerRecorder UsedBuffersBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Buffers Bytes");
    ProfilerRecorder VertexBufferUploadInFrameBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertex Buffer Upload In Frame Bytes");
    ProfilerRecorder IndexBufferUploadInFrameBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Index Buffer Upload In Frame Bytes");
    Reporter s_Reporter;
    Reporter Reporter
    {
        get
        {
            if (s_Reporter == null)
                s_Reporter = FindObjectOfType<Reporter>();

            return s_Reporter;
        }
    }

    public long GetRenderNumber(int category)
    {
        var categoryEnum = (RenderCategory)category;
        switch (categoryEnum)
        {
            case RenderCategory.SetPassCallsCount:
                return SetPassCallsCount.LastValue;
            case RenderCategory.DrawCallsCount:
                return DrawCallsCount.LastValue;
            case RenderCategory.TotalBatchesCount:
                return TotalBatchesCount.LastValue;
            case RenderCategory.TrianglesCount://3
                return TrianglesCount.LastValue;
            case RenderCategory.VerticesCount://4
                return VerticesCount.LastValue;
            case RenderCategory.FPSCount:
                return (long)(Reporter?.fps ?? 0);
            case RenderCategory.ShadowCasters:
                return ShadowCasters.LastValue;
            case RenderCategory.VisibleSkinnedMeshes:
                return VisibleSkinnedMeshes.LastValue;
            case RenderCategory.DynamicBatchCount:
                return DynamicBatchCount.LastValue;
            case RenderCategory.BatchCount:
                return BatchCount.LastValue;
            case RenderCategory.InstancedBatchCount:
                return InstancedBatchCount.LastValue;
            case RenderCategory.RenderTexture:
                return RenderTexturesBytes.LastValue;
            case RenderCategory.UsedBuffer:
                return UsedBuffersBytes.LastValue;
            case RenderCategory.VertexBufferInFrame:
                return VertexBufferUploadInFrameBytes.LastValue;
            case RenderCategory.IndexBufferInFrame:
                return IndexBufferUploadInFrameBytes.LastValue;
            default:
                return 0;
        }
    }
}

public enum RenderCategory
{
    FPSCount = 0,
    DrawCallsCount = 1,
    TotalBatchesCount = 2,
    TrianglesCount = 3,
    VerticesCount = 4,
    SetPassCallsCount = 5,
    ShadowCasters = 6,
    VisibleSkinnedMeshes = 7,
    DynamicBatchCount = 8,
    BatchCount = 9,
    InstancedBatchCount = 10,
    RenderTexture = 11,
    UsedBuffer = 12,
    VertexBufferInFrame = 13,
    IndexBufferInFrame = 14,
}
