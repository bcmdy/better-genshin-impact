# textToImage 文字转图片

生成文字图片供JS脚本调用，可用于模板匹配、OCR预处理等场景。

## 可用字体

- `HYW` - 原神风格字体（默认）
- `Fgi-Regular` - FGI常规字体
- `MiSans-Regular` - MiSans字体
- `deluge-led` - LED数字字体

## 方法

### generate(text, fontName?, fontSize?, textColor?, bgColor?)

生成PNG图片，返回 `byte[]` 二进制数据。

**参数：**
- `text` (string) - 要生成的文字
- `fontName` (string, optional) - 字体名称，默认 `HYW`
- `fontSize` (number, optional) - 字体大小，默认 `24`
- `textColor` (string, optional) - 文字颜色(hex)，默认 `#505769`
- `bgColor` (string, optional) - 背景颜色(hex)，默认 `#F5F6F7`

**返回：** `byte[]` PNG格式二进制数据

### generateMat(text, fontName?, fontSize?, textColor?, bgColor?)

生成 `OpenCvSharp.Mat` 图片，用于模板匹配。

**参数：** 同 `generate`

**返回：** `Mat`

### getAvailableFonts()

获取可用字体列表。

**返回：** `string[]`

### generateFromFontFile(text, fontFilePath, fontSize?, textColor?, bgColor?)

从字体文件生成图片，返回 `byte[]`。

**参数：**
- `text` (string) - 要生成的文字
- `fontFilePath` (string) - 字体文件路径(.ttf/.ttc)
- `fontSize` (number, optional) - 字体大小，默认 `24`
- `textColor` (string, optional) - 文字颜色(hex)，默认 `#505769`
- `bgColor` (string, optional) - 背景颜色(hex)，默认 `#F5F6F7`

**返回：** `byte[]` PNG格式二进制数据

### generateMatFromFontFile(text, fontFilePath, fontSize?, textColor?, bgColor?)

从字体文件生成图片，返回 `Mat`。

### generateMatFromFontBytes(text, fontBytes, fontSize?, textColor?, bgColor?)

从字体字节数据生成图片，返回 `Mat`。

**参数：**
- `fontBytes` (byte[]) - 字体字节数据

## 使用示例

### 示例1：生成PNG图片并保存

```javascript
// 生成PNG二进制数据并保存到文件
var imgData = textToImage.generate("提瓦特");
file.WriteBytes("test.png", imgData);
```

### 示例2：生成Mat用于模板匹配

```javascript
// 生成Mat用于模板匹配
var mat = textToImage.generateMat("莉奈娅", "HYW", 24, "#000000", "#FFFFFF");

// 使用Mat进行模板匹配
var templateRo = RecognitionObject.TemplateMatch(mat);
var region = captureGameRegion();
var result = region.Find(templateRo);
region.Dispose();

// 使用完毕后释放资源
mat.Dispose();
```

### 示例3：批量生成玩家名字模板

```javascript
// 批量生成白名单玩家名字模板
var playerNames = ["空", "荧", "旅行者", "派蒙"];
var templates = {};

for (var i = 0; i < playerNames.length; i++) {
    var name = playerNames[i];
    templates[name] = textToImage.generateMat(name, "HYW", 24, "#505769", "#F5F6F7");
    log.info("已生成玩家模板: " + name + ", 尺寸: " + templates[name].Width + "x" + templates[name].Height);
}

// 在任务中使用
// ...

// 完成后释放
for (var name in templates) {
    templates[name].Dispose();
}
```

### 示例4：使用不同颜色和字体

```javascript
// 红色文字配透明背景（白色）
var mat1 = textToImage.generateMat("警告", "HYW", 28, "#FF0000", "#FFFFFF");

// 金色文字
var mat2 = textToImage.generateMat("+999", "deluge-led", 20, "#FFD700", "#000000");

// 大号字体
var mat3 = textToImage.generateMat("标题", "MiSans-Regular", 36, "#000000", "#FFFFFF");

mat1.Dispose();
mat2.Dispose();
mat3.Dispose();
```

### 示例5：使用外部字体文件生成图片

```javascript
// 从字体文件路径生成
var fontPath = "C:/Windows/Fonts/msyh.ttc";
var mat = textToImage.generateMatFromFontFile("测试文字", fontPath, 24, "#000000", "#FFFFFF");

// 完成后释放
mat.Dispose();
```

### 示例6：使用字体字节数据生成图片

```javascript
// 从文件读取字体字节
var fontBytes = file.ReadBytes("assets/custom.ttf");

// 使用字体字节生成图片
var mat = textToImage.generateMatFromFontBytes("自定义字体", fontBytes, 24, "#FF0000", "#FFFFFF");
log.info("生成的图片尺寸: " + mat.Width + "x" + mat.Height);

mat.Dispose();
```

## 颜色参考

| 颜色值 | 效果 |
|-------|------|
| `#505769` | 原神默认文字颜色（灰蓝色） |
| `#000000` | 黑色 |
| `#FFFFFF` | 白色 |
| `#FFD700` | 金色 |
| `#FF6B6B` | 红色 |
| `#4ECDC4` | 青色 |