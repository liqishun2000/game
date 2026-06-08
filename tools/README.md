# 资源生成工具

生成游戏内占位/像素素材，输出到 `MauiApp/Resources/Raw/`。

```bash
python tools/gen_audio.py   # audio/*.wav（BGM + SFX）
python tools/gen_art.py     # art/tiles、art/units、art/portraits
```

更换正式素材时，保持相同路径与文件名即可被 `AudioKeys` / `GfxKeys` 直接加载。
