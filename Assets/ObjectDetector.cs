using UnityEngine;
using Unity.Barracuda;

namespace TinyYoloV2 {

sealed class ObjectDetector : MonoBehaviour
{
    #region Editable attributes

    [SerializeField, HideInInspector] NNModel _model = null;
    [SerializeField, HideInInspector] ComputeShader _compute = null;
    [SerializeField, HideInInspector] Shader _visualizerShader = null;

    #endregion

    #region Compile-time constants

    // Pre-defined constants from out Tiny YOLOv2 model
    const int ImageSize = 416;
    const int CellsInRow = 13;
    const int ClassCount = 20;
    const int AnchorCount = 5;

    const int InputTensorSize = ImageSize * ImageSize * 3;
    const int OutputDataCount = CellsInRow * CellsInRow * AnchorCount;

    #endregion

    #region Internal objects

    WebCamTexture _webcamRaw;
    RenderTexture _webcamBuffer;
    ComputeBuffer _preBuffer;
    ComputeBuffer _postBuffer;
    Material _visualizer;
    IWorker _worker;

    #endregion

    #region MonoBehaviour implementation

    void Start()
    {
        _webcamRaw = new WebCamTexture();
        _webcamBuffer = new RenderTexture(1920, 1080, 0);
        _preBuffer = new ComputeBuffer(InputTensorSize, sizeof(float));
        _postBuffer = new ComputeBuffer(OutputDataCount, sizeof(float) * 6);
        _visualizer = new Material(_visualizerShader);
        _worker = ModelLoader.Load(_model).CreateWorker();

        _webcamRaw.Play();
    }

    void OnDisable()
    {
        _preBuffer?.Dispose();
        _preBuffer = null;

        _postBuffer?.Dispose();
        _postBuffer = null;

        _worker?.Dispose();
        _worker = null;
    }

    void OnDestroy()
    {
        if (_webcamRaw != null) Destroy(_webcamRaw);
        if (_webcamBuffer != null) Destroy(_webcamBuffer);
        if (_visualizer != null) Destroy(_visualizer);
    }

    void Update()
    {
        // Do nothing if there is no update on the webcam.
        if (!_webcamRaw.didUpdateThisFrame) return;

        // Input buffer update
        var vflip = _webcamRaw.videoVerticallyMirrored;
        var scale = new Vector2(1, vflip ? -1 : 1);
        var offset = new Vector2(0, vflip ? 1 : 0);
        Graphics.Blit(_webcamRaw, _webcamBuffer, scale, offset);

        // Preprocessing
        _compute.SetTexture(0, "_Texture", _webcamBuffer);
        _compute.SetBuffer(0, "_Tensor", _preBuffer);
        _compute.SetInt("_ImageSize", ImageSize);
        _compute.Dispatch(0, ImageSize / 8, ImageSize / 8, 1);

        // YOLO execution
        using (var tensor = new Tensor(1, ImageSize, ImageSize, 3, _preBuffer))
            _worker.Execute(tensor);

        // Postprocessing
        var output = _worker.PeekOutput();
        var reshape = new TensorShape
          (1, CellsInRow * CellsInRow, AnchorCount * (5 + ClassCount), 1);

        using (var reshaped = output.Reshape(reshape))
        {
            var reshapedRT = RenderTexture.GetTemporary
              (reshape.width, reshape.height, 0, RenderTextureFormat.RFloat);

            reshaped.ToRenderTexture(reshapedRT);

            _compute.SetTexture(1, "_Input", reshapedRT);
            _compute.SetBuffer(1, "_Output", _postBuffer);
            _compute.Dispatch(1, 1, 1, 1);

            RenderTexture.ReleaseTemporary(reshapedRT);
        }
    }

    void OnPostRender()
    {
        _visualizer.SetPass(0);
        _visualizer.SetTexture("_CameraFeed", _webcamBuffer);
        Graphics.DrawProceduralNow(MeshTopology.Quads, 4, 1);

        _visualizer.SetPass(1);
        _visualizer.SetBuffer("_Boxes", _postBuffer);
        Graphics.DrawProceduralNow
          (MeshTopology.Quads, 4, OutputDataCount);
    }

    #endregion
}

} // namespace TinyYoloV2
