
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Profiling.Memory.Experimental;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Object = UnityEngine.Object;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_MEMOERY_PROFILER_EDITOR
using Unity.MemoryProfiler.Editor.Format;
#endif


public sealed class ProfilerAPI : MonoBehaviour
{

    ProfilerAPI s_Instance;

    public ProfilerAPI Instance
    {
        get
        {
            if (s_Instance == null)
            {
                s_Instance = FindObjectOfType<ProfilerAPI>();
                GameObject go = null;

                if (!s_Instance)
                {
                    go = new GameObject(nameof(ProfilerAPI));
                    s_Instance = go.AddComponent<ProfilerAPI>();
                }
                else
                {
                    go = s_Instance.gameObject;
                }

#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
#endif
                    DontDestroyOnLoad(go);
            }

            return s_Instance;
        }
    }

    string CaptureFolderPath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "UserMemoryCaptures");
        }
    }


    // 快照标记类型
    int Capture_Flags = (int)(UserCaptureFlags.ManagedObjects | UserCaptureFlags.NativeObjects);

    // 内存数据
    SimpleData Data = new SimpleData()
    {
        Textures = new MemoryInfo(),
        Texture2Ds = new MemoryInfo(),
        Meshes = new MemoryInfo(),
        Materials = new MemoryInfo(),
        AnimationClips = new MemoryInfo(),
        AudioClips = new MemoryInfo(),
        Fonts = new MemoryInfo(),
        Cubemaps = new MemoryInfo(),
        Shaders = new MemoryInfo(),
        GameObjectCount = 0,
        ObjectCount = 0,
        StreamingTexture = new MemoryInfo(),
        NonStreamingTexture = new MemoryInfo(),
    };


    /**
     * 当请求分配16个字节时，Unity会要求系统分配 4MB（Player）或 16MB（Editor） 内存块，然后后续所有分配都使用该内存，如果内存块满了，
     * 则分配另一块，如果块为空，则将其返回给系统。所以这会让统计数据看起来有点混乱。
     * 
     **/
    ProfilerRecorder SystemUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
    ProfilerRecorder TotalUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
    ProfilerRecorder TotalReservedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
    ProfilerRecorder GCUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
    ProfilerRecorder GCReservedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
    ProfilerRecorder GfxReservedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Reserved Memory");
    ProfilerRecorder GfxUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");

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

    ProfilerRecorder RenderTexturesCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Count");
    ProfilerRecorder RenderTexturesBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes");
    ProfilerRecorder UsedBuffersCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Buffers Count");
    ProfilerRecorder UsedBuffersBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Buffers Bytes");
    ProfilerRecorder VertexBufferUploadInFrameCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertex Buffer Upload In Frame Count");
    ProfilerRecorder VertexBufferUploadInFrameBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertex Buffer Upload In Frame Bytes");
    ProfilerRecorder IndexBufferUploadInFrameCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Index Buffer Upload In Frame Count");
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

    bool ShowFPS;

    #region Editor
#if UNITY_EDITOR

    // Scene视图绘制模式
    //[LuaInterface.NoToLua]
    public CustomDrawCameraMode CameraMode
    {
        get
        {
            return (CustomDrawCameraMode)SceneView.lastActiveSceneView.renderMode;
        }
        set
        {
            SceneView.lastActiveSceneView.renderMode = (DrawCameraMode)value;
            SceneView.lastActiveSceneView.Repaint();
        }
    }

#endif
    #endregion

    #region Runtime

    GUIStyle m_LabelStyle;
    // 只在开发模式下才有效
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void OnGUI()
    {
        if (ShowFPS)
        {
            if (m_LabelStyle == null)
            {
                m_LabelStyle = new GUIStyle(GUI.skin.label);
                m_LabelStyle.fontSize = 30;
            }

            var rect = new Rect();
            rect.width = 300;
            rect.height = 50;
            GUILayout.Label("FPS ：" + GetRenderNumberFormatDesc((int)ProfilerAPI.RenderCategory.FPSCount), m_LabelStyle);
        }
    }

    #endregion Runtime

    [Button("内存快照")]
    public void Capture(int flags = 0)
    {
        var capturePath = CaptureFolderPath;

        if (!Directory.Exists(capturePath))
            Directory.CreateDirectory(capturePath);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        UnityEngine.Profiling.Memory.Experimental.CaptureFlags captureFlags;
        if (flags == 0)
            captureFlags = (UnityEngine.Profiling.Memory.Experimental.CaptureFlags)(uint)Capture_Flags;
        else
            captureFlags = (UnityEngine.Profiling.Memory.Experimental.CaptureFlags)(uint)flags;

        MemoryProfiler.TakeSnapshot(Path.Combine(capturePath, string.Format("Snapshot_{0}.snap", DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"))), (path, result) =>
        {
            if (result)
                Debug.LogFormat("内存快照成功. 快照文件存储在: {0}", path);
            else
                Debug.LogError("内存快照失败");

        }, captureFlags);
    }

    [Button("打开快照列表")]
    public void OpenSnapshotFolder()
    {
#if UNITY_EDITOR && UNITY_MEMOERY_PROFILER_EDITOR
        var snapFiles = Directory.GetFiles(CaptureFolderPath, "*.snap", SearchOption.TopDirectoryOnly).ToList();
        var snapNames = snapFiles.ConvertAll(x => Path.GetFileName(x));

        Shortcuts.OpenIntSelector(-1, snapNames, (index, name) =>
        {
            var path = Path.Combine(CaptureFolderPath, name);
            var snapshot = QueriedMemorySnapshot.Load(path);
            Debug.LogError(snapshot.recordDate);

            var nativeObjects = snapshot.nativeObjects;
            var nativeObjectCount = nativeObjects.GetNumEntries();

            var nativeObjectNames = new string[nativeObjectCount];
            nativeObjects.objectName.GetEntries(0, nativeObjectCount, ref nativeObjectNames);

            Debug.LogError(nativeObjectNames.Length);

            //TODO 打开快照列表

        }, new Vector2(150, 50));
#endif
    }

    [Button("获取系统内存值")]
    public long GetSystemMemoryNumber(int category)
    {
        var categoryEnum = (SystemMemoryCategory)category;
        switch (categoryEnum)
        {
            case SystemMemoryCategory.SystemUsedMemory:
                return SystemUsedMemory.LastValue;
            case SystemMemoryCategory.TotalUsedMemory:
                return TotalUsedMemory.LastValue;
            case SystemMemoryCategory.TotalReservedMemory:
                return TotalReservedMemory.LastValue;
            case SystemMemoryCategory.GCUsedMemory:
                return GCUsedMemory.LastValue;
            case SystemMemoryCategory.GCReservedMemory:
                return GCReservedMemory.LastValue;
            case SystemMemoryCategory.GfxUsedMemory:
                return GfxUsedMemory.LastValue;
            case SystemMemoryCategory.GfxReservedMemory:
                return GfxReservedMemory.LastValue;
            default:
                return 0L;
        }
    }

    [Button("获取系统内存值格式化文本")]
    public string GetSystemMemoryFormatDesc(int category)
    {
        var number = GetSystemMemoryNumber(category);

        return FormatBytes(number);
    }

    [Button("获取资源内存值格式化文本")]
    public string GetResourceMemoryFormatDesc(int category)
    {
        var data = Data;
        var categoryEnum = (ResourceMemoryCategory)category;

        switch (categoryEnum)
        {
            case ResourceMemoryCategory.Textures:
                return data.Textures.ToString();
            case ResourceMemoryCategory.Texture2Ds:
                return data.Texture2Ds.ToString();
            case ResourceMemoryCategory.Meshes:
                return data.Meshes.ToString();
            case ResourceMemoryCategory.Materials:
                return data.Materials.ToString();
            case ResourceMemoryCategory.AnimationClips:
                return data.AnimationClips.ToString();
            case ResourceMemoryCategory.Shaders:
                return data.Shaders.ToString();
            case ResourceMemoryCategory.Cubemaps:
                return data.Cubemaps.ToString();
            case ResourceMemoryCategory.AudioClips:
                return data.AudioClips.ToString();
            case ResourceMemoryCategory.Fonts:
                return data.Fonts.ToString();
            case ResourceMemoryCategory.GameObjectCount:
                return data.GameObjectCount.ToString();
            case ResourceMemoryCategory.ObjectCount:
                return data.ObjectCount.ToString();
            case ResourceMemoryCategory.StreamingTexture:
                return data.StreamingTexture.ToString();
            case ResourceMemoryCategory.NonStreamingTexture:
                return data.NonStreamingTexture.ToString();
            default:
                return string.Empty;
        }
    }

    [Button("获取渲染值")]
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
            case RenderCategory.TrianglesCount:
                return TrianglesCount.LastValue;
            case RenderCategory.VerticesCount:
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

    [Button("获取渲染值格式化文本")]
    public string GetRenderNumberFormatDesc(int category)
    {
        var categoryEnum = (RenderCategory)category;
        var number = GetRenderNumber(category);

        switch (categoryEnum)
        {
            case RenderCategory.FPSCount:
                return number.ToString("00");
            case RenderCategory.RenderTexture:
                return string.Format("{0}/{1}", RenderTexturesCount.LastValue, FormatBytes(RenderTexturesBytes.LastValue));
            case RenderCategory.UsedBuffer:
                return string.Format("{0}/{1}", UsedBuffersCount.LastValue, FormatBytes(UsedBuffersBytes.LastValue));
            case RenderCategory.VertexBufferInFrame:
                return string.Format("{0}/{1}", VertexBufferUploadInFrameCount.LastValue, FormatBytes(VertexBufferUploadInFrameBytes.LastValue));
            case RenderCategory.IndexBufferInFrame:
                return string.Format("{0}/{1}", IndexBufferUploadInFrameCount.LastValue, FormatBytes(IndexBufferUploadInFrameBytes.LastValue));
            default:
                return FormatNumber(number);
        }
    }

    [Button("获取系统内存类型名")]
    public string GetSystemMemoryName(int category)
    {
        var categoryEnum = (SystemMemoryCategory)category;
        switch (categoryEnum)
        {
            case SystemMemoryCategory.SystemUsedMemory:
                //包含未被Unity跟踪的内存
                return "游戏进程的总内存";
            case SystemMemoryCategory.TotalUsedMemory:
                // Unity使用和跟踪的内存
                return "Unity使用的内存";
            case SystemMemoryCategory.TotalReservedMemory:
                // 包含已使用和跟踪的内存
                return "Unity预留的内存";
            case SystemMemoryCategory.GCUsedMemory:
                //可被垃圾回收
                return "GC使用的内存";
            case SystemMemoryCategory.GCReservedMemory:
                return "GC预留的内存";
            case SystemMemoryCategory.GfxUsedMemory:
                return "Gfx使用的内存";
            case SystemMemoryCategory.GfxReservedMemory:
                return "Gfx预留的内存";
            default:
                return string.Empty;
        }
    }

    [Button("获取资源类型名")]
    public string GetResourceMemoryName(int category)
    {
        var categoryEnum = (ResourceMemoryCategory)category;
        switch (categoryEnum)
        {
            case ResourceMemoryCategory.Textures:
                return "贴图";
            case ResourceMemoryCategory.Texture2Ds:
                return "2D贴图";
            case ResourceMemoryCategory.Meshes:
                return "网格";
            case ResourceMemoryCategory.Materials:
                return "材质球";
            case ResourceMemoryCategory.AnimationClips:
                return "动画Clip";
            case ResourceMemoryCategory.Shaders:
                return "Shader";
            case ResourceMemoryCategory.Cubemaps:
                return "Cubemap";
            case ResourceMemoryCategory.AudioClips:
                return "音频";
            case ResourceMemoryCategory.Fonts:
                return "字体";
            case ResourceMemoryCategory.GameObjectCount:
                return "游戏对象数量";
            case ResourceMemoryCategory.ObjectCount:
                return "所有对象数量";
            case ResourceMemoryCategory.StreamingTexture:
                return "串流贴图";
            case ResourceMemoryCategory.NonStreamingTexture:
                return "非串流贴图";
            default:
                return string.Empty;
        }
    }

    [Button("获取渲染类型名")]
    public string GetRenderName(int category)
    {
        var categoryEnum = (RenderCategory)category;
        switch (categoryEnum)
        {
            case RenderCategory.FPSCount:
                return "FPS";
            case RenderCategory.SetPassCallsCount:
                return "SetPass";
            case RenderCategory.DrawCallsCount:
                return "DrawCalls";
            case RenderCategory.TotalBatchesCount:
                return "Batches";
            case RenderCategory.TrianglesCount:
                return "三角形数";
            case RenderCategory.VerticesCount:
                return "顶点数";
            case RenderCategory.ShadowCasters:
                return "Shadow Casters";
            case RenderCategory.VisibleSkinnedMeshes:
                return "Visible Skinned Meshes";
            case RenderCategory.DynamicBatchCount:
                return "Dynamic Batch Draw Calls";
            case RenderCategory.BatchCount:
                return " Batch Draw Calls";
            case RenderCategory.InstancedBatchCount:
                return "Instanced Batch Draw Calls";
            case RenderCategory.RenderTexture:
                return "Render Texture";
            case RenderCategory.UsedBuffer:
                return "Used Buffer";
            case RenderCategory.VertexBufferInFrame:
                return "Vertex Buffer Frame";
            case RenderCategory.IndexBufferInFrame:
                return "Index Buffer Frame";
            default:
                return string.Empty;
        }
    }

    [Button("获取当前帧资源内存")]
    public SimpleData GetResourceMemoryInfos()
    {
        var objs = Resources.FindObjectsOfTypeAll<Object>();
        var data = Data;
        data.Reset();

        foreach (var item in objs)
        {
            if (item is Texture)
            {
                var memory = GetRuntimeMemorySizeLong(item); ;
                data.Textures.AddObject(item, memory);

                if (!item.hideFlags.HasFlag(HideFlags.DontSave))
                {
                    if (item is Texture2D)
                        data.Texture2Ds.AddObject(item, memory);
                    else if (item is Cubemap)
                        data.Cubemaps.AddObject(item, memory);
                }
            }
            else if (item is Mesh)
            {
                data.Meshes.AddObject(item);
            }
            else if (item is Material)
            {
                data.Materials.AddObject(item);
            }
            else if (item is AnimationClip)
            {
                data.AnimationClips.AddObject(item);
            }
            else if (item is AudioClip)
            {
                data.AudioClips.AddObject(item);
            }
            else if (item is Font)
            {
                data.Fonts.AddObject(item);
            }
            else if (item is Shader)
            {
                data.Shaders.AddObject(item);
            }
            else if (item is GameObject go)
            {
                if (!go.hideFlags.HasFlag(HideFlags.HideInHierarchy | HideFlags.HideInInspector))
                    data.GameObjectCount++;
            }
        }

        data.ObjectCount = objs.Length;
        data.NonStreamingTexture.SetData((int)Texture.nonStreamingTextureCount, (long)Texture.nonStreamingTextureMemory);
        data.StreamingTexture.SetData((int)Texture.streamingTextureCount, 0);

        data.CalcuateMemory();

        return data;
    }

    [Button("获取所有可用ProfilerRecorder")]
    public void GetAvailable()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);

        var list = new List<StatInfo>(handles.Count);
        foreach (var handle in handles)
        {
            var handleDesc = ProfilerRecorderHandle.GetDescription(handle);
            list.Add(new StatInfo { Name = handleDesc.Name, Category = handleDesc.Category, Unit = handleDesc.UnitType });
        }

        list.Sort((x, y) =>
        {
            var result = string.Compare(x.Category.ToString(), y.Category.ToString());
            if (result != 0)
                return result;

            return string.Compare(x.Name, y.Name);
        });

        var sb = new StringBuilder();
        foreach (var item in list)
            sb.AppendFormat("Category:{0} Name:{1} Unit:{2}\n", item.Category, item.Name, item.Unit);

        var fileName = "Assets/ProfilerRecorderHandles.txt";
        File.WriteAllText(fileName, sb.ToString());

#if UNITY_EDITOR
        AssetDatabase.Refresh();
        var obj = AssetDatabase.LoadAssetAtPath<Object>(fileName);
        if (obj)
            AssetDatabase.OpenAsset(obj);
#endif
    }

    [Button("开关FPS")]
    public void ToggleFPS()
    {
        ShowFPS = !ShowFPS;
    }

    public static long GetRuntimeMemorySizeLong(Object @object)
    {
#if UNITY_EDITOR
        if (Application.platform == RuntimePlatform.WindowsEditor)
            return ProfilerUtils.GetStorageMemorySizeLong(@object);
#endif
        return ProfilerUtils.GetRuntimeMemorySizeLong(@object);
    }

    public IEnumerator<Object> FindObjectsOfTypeAll<T>() where T : Object
    {
        var objs = Resources.FindObjectsOfTypeAll<T>();
        foreach (var obj in objs)
        {
            if (!obj.hideFlags.HasFlag(HideFlags.DontSave))
                yield return obj;
        }
    }

    public static string FormatBytes(long bytes)
    {
        return FormatBytes((ulong)bytes);
    }

    static string FormatBytes(ulong bytes)
    {
        const float KB = 1024;
        const float MB = KB * 1024;
        const float GB = MB * 1024;

        if (bytes < KB)
        {
            return string.Format("{0:F2} B", bytes);
        }
        else if (bytes < MB)
        {
            return string.Format("{0:F2} KB", bytes / KB);
        }
        else if (bytes < GB)
        {
            return string.Format("{0:F2} MB", bytes / MB);
        }
        else
        {
            return string.Format("{0:F2} GB", bytes / GB);
        }
    }

    public string FormatNumber(long num)
    {
        return FormatNumber((ulong)num);
    }

    string FormatNumber(ulong num)
    {
        if (num < 1000)
            return num.ToString(CultureInfo.InvariantCulture.NumberFormat);

        if (num < 1000000)
            return (num * 0.001).ToString("F1", CultureInfo.InvariantCulture.NumberFormat) + "k";

        return (num * 0.000001).ToString("F1", CultureInfo.InvariantCulture.NumberFormat) + "M";
    }

    public string FormatTime(long num)
    {
        return (num * (1e-6f)).ToString("F1");
    }

    [ShowInInspector]
    [Serializable]
    public class SimpleData
    {
        [LabelText("贴图")]
        [ShowInInspector]
        public MemoryInfo Textures { set; get; }

        [LabelText("2D贴图")]
        [ShowInInspector]
        public MemoryInfo Texture2Ds { set; get; }

        [LabelText("网格")]
        [ShowInInspector]
        public MemoryInfo Meshes { set; get; }

        [LabelText("材质球")]
        [ShowInInspector]
        public MemoryInfo Materials { set; get; }

        [LabelText("动画Clip")]
        [ShowInInspector]
        public MemoryInfo AnimationClips { set; get; }

        [LabelText("Shader")]
        [ShowInInspector]
        public MemoryInfo Shaders { set; get; }

        [LabelText("Cubemap")]
        [ShowInInspector]
        public MemoryInfo Cubemaps { set; get; }

        [LabelText("音频")]
        [ShowInInspector]
        public MemoryInfo AudioClips { set; get; }

        [LabelText("字体")]
        [ShowInInspector]
        public MemoryInfo Fonts { set; get; }

        [LabelText("游戏对象数量")]
        [ShowInInspector]
        public int GameObjectCount { set; get; }

        [LabelText("对象数量")]
        [ShowInInspector]
        public int ObjectCount { set; get; }

        [LabelText("StreamingTexture")]
        [ShowInInspector]
        public MemoryInfo StreamingTexture { set; get; }

        [LabelText("NonStreamingTexture")]
        [ShowInInspector]
        public MemoryInfo NonStreamingTexture { set; get; }


        public void CalcuateMemory()
        {
            Textures.CalcuateMemory();
            Texture2Ds.CalcuateMemory();
            Meshes.CalcuateMemory();
            Materials.CalcuateMemory();
            AnimationClips.CalcuateMemory();
            Shaders.CalcuateMemory();
            Cubemaps.CalcuateMemory();
            AudioClips.CalcuateMemory();
            Fonts.CalcuateMemory();
            StreamingTexture.CalcuateMemory();
            NonStreamingTexture.CalcuateMemory();
        }

        public void Reset()
        {
            Textures.Reset();
            Texture2Ds.Reset();
            Meshes.Reset();
            Materials.Reset();
            AnimationClips.Reset();
            Shaders.Reset();
            Cubemaps.Reset();
            AudioClips.Reset();
            Fonts.Reset();
            GameObjectCount = 0;
            ObjectCount = 0;

            StreamingTexture.Reset();
            NonStreamingTexture.Reset();
        }
    }

    [ShowInInspector]
    [Serializable]
    public class MemoryInfo
    {
        string m_Cache;

        [LabelText("数量")]
        [ShowInInspector]
        public int Count { private set; get; }

        [LabelText("内存值")]
        [ShowInInspector]
        public long Size { private set; get; }

        [LabelText("内存描述")]
        [ShowInInspector]
        public string SizeDesc { private set; get; }

        [LabelText("对象资源列表")]
        //[LuaInterface.NoToLua]
        public List<Object> Objects { private set; get; }


        public MemoryInfo()
        {
            Objects = new List<Object>();
        }


        public void CalcuateMemory()
        {
            SizeDesc = FormatBytes(Size);
            m_Cache = string.Format("{0}/{1}", Count, SizeDesc);
        }

        public void AddObject(Object item, long memory = 0)
        {
            Count++;
            Size += memory != 0 ? memory : GetRuntimeMemorySizeLong(item);

            Objects.Add(item);
        }

        public void SetData(int count, long memory)
        {
            Count = count;
            Size = memory;
        }


        public void Reset()
        {
            Count = 0;
            Size = 0;
            SizeDesc = string.Empty;
            m_Cache = string.Empty;
            Objects.Clear();
        }


        public override string ToString()
        {
            return m_Cache;
        }
    }

    struct StatInfo
    {
        public string Name;
        public ProfilerCategory Category;
        public ProfilerMarkerDataUnit Unit;
    }

    public enum UserCaptureFlags : uint
    {
        ManagedObjects = 1,
        NativeObjects = 2,
        NativeAllocations = 4,
        NativeAllocationSites = 8,
        NativeStackTraces = 16
    }

    public enum SystemMemoryCategory : int
    {
        SystemUsedMemory = 0,
        TotalUsedMemory = 1,
        TotalReservedMemory = 2,
        GCUsedMemory = 3,
        GCReservedMemory = 4,
        GfxUsedMemory = 5,
        GfxReservedMemory = 6,
    }

    public enum ResourceMemoryCategory
    {
        Textures = 0,
        Texture2Ds = 1,
        Meshes = 2,
        Materials = 3,
        AnimationClips = 4,
        Shaders = 5,
        Cubemaps = 6,
        AudioClips = 7,
        Fonts = 8,
        GameObjectCount = 9,
        ObjectCount = 10,
        StreamingTexture = 11,
        NonStreamingTexture = 12,
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

#if UNITY_EDITOR

    //[LuaInterface.NoToLua]
    public enum CustomDrawCameraMode
    {
        UserDefined = int.MinValue,
        Baked = -18,
        Directionality = -17,
        Irradiance = -16,
        Emissive = -15,
        Albedo = -14,
        Charting = -12,
        Normal = -1,
        Textured = 0,
        Wireframe = 1,
        TexturedWire = 2,
        ShadowCascades = 3,
        RenderPaths = 4,
        AlphaChannel = 5,
        Overdraw = 6,
        Mipmaps = 7,
        DeferredDiffuse = 8,
        DeferredSpecular = 9,
        DeferredSmoothness = 10,
        DeferredNormal = 11,
        RealtimeCharting = 12,
        Systems = 13,
        RealtimeAlbedo = 14,
        RealtimeEmissive = 15,
        RealtimeIndirect = 16,
        RealtimeDirectionality = 17,
        BakedLightmap = 18,
        Clustering = 19,
        LitClustering = 20,
        ValidateAlbedo = 21,
        ValidateMetalSpecular = 22,
        ShadowMasks = 23,
        LightOverlap = 24,
        BakedAlbedo = 25,
        BakedEmissive = 26,
        BakedDirectionality = 27,
        BakedTexelValidity = 28,
        BakedIndices = 29,
        BakedCharting = 30,
        SpriteMask = 31,
        BakedUVOverlap = 32,
        TextureStreaming = 33,
        BakedLightmapCulling = 34,
        GIContributorsReceivers = 35
    }
#endif
}


public class ProfilerUtils
{
#if UNITY_EDITOR
    static Func<Texture, long> s_GetStorageMemorySizeLongFunc;

    static ProfilerUtils()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.TextureUtil");
        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
        foreach (var method in methods)
        {
            if (method.Name == "GetStorageMemorySizeLong" && method.ReturnType == typeof(long))
            {
                s_GetStorageMemorySizeLongFunc = (Func<Texture, long>)Delegate.CreateDelegate(typeof(Func<Texture, long>), method);
                break;
            }
        }
    }
#endif


    public static long GetRuntimeMemorySizeLong(Object obj)
    {
        return !obj ? 0 : Profiler.GetRuntimeMemorySizeLong(obj);
    }


#if UNITY_EDITOR
    public static long GetStorageMemorySizeLong(Object obj)
    {
        var memorySize = 0L;
        if (obj is Texture2D || obj is Cubemap)
        {
            var texture2D = obj as Texture2D;
            memorySize = s_GetStorageMemorySizeLongFunc?.Invoke(obj as Texture) ?? 0;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(obj));
            if (importer is TextureImporter textureImporter)
            {
                // 如果格式在目标平台不支持时，则获取 FallBack 情况下的贴图内存
                var buildTarget = BuildTarget.Android;
                if (textureImporter.GetPlatformTextureSettings(buildTarget.ToString(), out var maxTextureSize, out var textureImporterFormat, out var textureCompressionQuality))
                    if (texture2D != null && !IsSupportFormat(buildTarget, (TextureFormat)textureImporterFormat))
                        memorySize = GetFallBackTextureMemory(texture2D);

                // 开启 Read/Write 时，内存翻倍
                if (textureImporter.isReadable)
                    memorySize *= 2;
            }
        }
        else
        {
            memorySize = !obj ? 0 : Profiler.GetRuntimeMemorySizeLong(obj);
        }

        return memorySize;
    }

    public static bool CheckMemoryWarning(out bool isOutOfMemory)
    {
        isOutOfMemory = false;

        var freeMemory = PerformanceInfo.GetFreeMemoryPercent();
        var minFreeMemory = 0.2F;
        isOutOfMemory = freeMemory <= minFreeMemory;
        if (isOutOfMemory)
            return EditorUtility.DisplayDialog("提示", string.Format("内存已不足 {0}%，是否跳出资源加载逻辑", Mathf.RoundToInt(minFreeMemory * 100)), "OK");

        return isOutOfMemory;
    }


    static Dictionary<string, HashSet<TextureFormat>> s_NotSupportFormatDic = new Dictionary<string, HashSet<TextureFormat>>()
    {
        {
            BuildTarget.Android.ToString(),
            new HashSet<TextureFormat>()
            {
                TextureFormat.DXT5,
                TextureFormat.DXT5Crunched,
                TextureFormat.DXT1,
                TextureFormat.DXT1Crunched,
                TextureFormat.BC5,
                TextureFormat.BC4,
                TextureFormat.BC6H,
                TextureFormat.BC7,
                TextureFormat.PVRTC_RGB2,
                TextureFormat.PVRTC_RGB4,
                TextureFormat.PVRTC_RGBA2,
                TextureFormat.PVRTC_RGBA4,
            }
        },
        {
            BuildTarget.iOS.ToString(),
            new HashSet<TextureFormat>()
            {
                TextureFormat.DXT5,
                TextureFormat.DXT5Crunched,
                TextureFormat.DXT1,
                TextureFormat.DXT1Crunched,
                TextureFormat.BC5,
                TextureFormat.BC4,
                TextureFormat.BC6H,
                TextureFormat.BC7,
            }
        },
    };

    public static bool IsSupportFormat(BuildTarget target, TextureFormat format)
    {
        if (target != BuildTarget.StandaloneWindows)
        {
            if (s_NotSupportFormatDic.TryGetValue(target.ToString(), out var textureFormats))
            {
                return !textureFormats.Contains(format);
            }
        }

        return true;
    }

    public static long GetFallBackTextureMemory(Texture2D texture2D)
    {
        if (texture2D == null)
            return 0;

        var temp = new Texture2D(texture2D.width, texture2D.height, TextureFormat.RGBA32, texture2D.mipmapCount, false);
        var memorySize = temp.GetRawTextureData().Length;
        Object.DestroyImmediate(temp);

        return memorySize;
    }


    static class PerformanceInfo
    {
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPerformanceInfo([Out] out PerformanceInformation PerformanceInformation, [In] int Size);

        [StructLayout(LayoutKind.Sequential)]
        public struct PerformanceInformation
        {
            public int Size;
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonPaged;
            public IntPtr PageSize;
            public int HandlesCount;
            public int ProcessCount;
            public int ThreadCount;
        }

        public static Int64 GetPhysicalAvailableMemoryInMiB()
        {
            PerformanceInformation pi = new PerformanceInformation();
            if (GetPerformanceInfo(out pi, Marshal.SizeOf(pi)))
            {
                return Convert.ToInt64((pi.PhysicalAvailable.ToInt64() * pi.PageSize.ToInt64() / 1048576));
            }
            else
            {
                return -1;
            }

        }

        public static Int64 GetTotalMemoryInMiB()
        {
            PerformanceInformation pi = new PerformanceInformation();
            if (GetPerformanceInfo(out pi, Marshal.SizeOf(pi)))
            {
                return Convert.ToInt64((pi.PhysicalTotal.ToInt64() * pi.PageSize.ToInt64() / 1048576));
            }
            else
            {
                return -1;
            }

        }

        public static float GetFreeMemoryPercent()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var phav = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
                var tot = PerformanceInfo.GetTotalMemoryInMiB();
                var percentFree = (double)phav / tot;
                return (float)percentFree;
            }

            return 1;
        }
    }
#endif
}
