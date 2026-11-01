# Local inference selection and next benchmark

Research checked 2026-10-31. The target is the user's RTX 4070 Ti with 12 GB VRAM, i9-14900K and 128 GB RAM, while BlueStacks and the GUI remain responsive. No paid inference endpoint or recurring AI subscription is required.

## Implemented baseline

Windows OCR plus deterministic text rules is the working baseline. It needs no downloaded language-model weights and runs asynchronously behind screen-change and cadence gates. Real local integration measured 240 ms on a launcher capture and 203 ms on a Super Snail event notice, including persistence/replay checks. See [observation behavior and limitations](OBSERVATION.md). Installed Windows OCR languages determine engine availability. [Microsoft documentation](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.trycreatefromuserprofilelanguages?view=winrt-26100).

This selects a measured, functioning OCR path for the first release. It does not establish superiority over other OCR engines or vision-language models. No VLM runtime or weights were installed, integrated or benchmarked in this session. The app has no strategic language-model inference yet.

## Candidates to measure

| Candidate | Intended experiment | Current status |
| --- | --- | --- |
| Windows OCR + rules | Text, numeric labels, known screen phrases | Implemented; two real screen integration runs |
| Ollama Qwen3-VL 2B | Ambiguous-screen descriptions and structured proposals | Researched; untested |
| Ollama Qwen3-VL 4B | Compare recognition quality and latency against 2B | Researched; untested |
| llama.cpp with a supported compact 2B–4B VLM | Compare local server overhead, quantization and GPU offload | Researched; untested |
| ONNX Runtime with a suitable specialized OCR/detection model | Compare reliable repeated-element recognition | Runtime researched; model selection pending |

Ollama currently lists Qwen3-VL package sizes of 1.9 GB for 2B, 3.3 GB for 4B and 6.2 GB for 8B. These are download sizes. Runtime VRAM must be measured with the selected context, image inputs, quantization and concurrent BlueStacks workload. Start with the explicit 2B and 4B local tags; do not use a cloud tag or infer fit from package size. [Ollama model library](https://ollama.com/library/qwen3-vl).

llama.cpp supports multimodal local inference using supported model weights and a matching multimodal projector. Its local server and projector-offload options provide a second runtime to compare. Confirm support for the exact chosen model/export before downloading it. [llama.cpp multimodal documentation](https://github.com/ggml-org/llama.cpp/blob/master/docs/multimodal.md).

ONNX Runtime's CUDA execution provider offers NVIDIA GPU acceleration. A selected model, compatible CUDA/cuDNN versions and measured provider initialization are still required; runtime support alone supplies no OCR capability. Avoid installing a GPU stack until a candidate justifies it. [CUDA provider documentation](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html).

## Reproducible next experiment

Prepare a locally stored, labeled set covering home, exploration, minions, research, combat, loading screens and unexpected dialogs. Include both orientations, small text, unreadable regions and visually similar screens. Keep a held-out set separate from prompt/rule development.

Run 2B and 4B candidates first with bounded 4096- and 8192-token contexts, one screenshot per request, fixed output limits and one concurrent request. Record the exact runtime/model revision, quantization, context, image preprocessing, prompt, warmup policy and GPU driver. Measure:

- Screen classification accuracy, `Unknown` handling, numeric extraction errors and unsupported claims.
- Valid structured-output rate, correct framebuffer coordinates and proposal evidence.
- Cold load time, warm median/p95 latency, timeouts, peak VRAM/RAM and BlueStacks responsiveness.
- Cache reuse on unchanged screens and recovery after runtime shutdown or malformed output.

Ollama response metrics include load, prompt-evaluation, generation and total durations, plus input/output token counts; durations are in nanoseconds. Combine those metrics with wall-clock timing and independent GPU memory sampling. [Ollama usage metrics](https://docs.ollama.com/api/usage).

Choose the smallest candidate that improves held-out recognition at acceptable measured latency and memory use. Keep deterministic recognition and OCR for repeatable elements. An eventual VLM should interpret ambiguous evidence through a local adapter with bounded requests and caching. Its output must remain a proposal until separately verified. Persistent observations and decision feedback are account memory; actual model training would require its own measured implementation.
