// 测试 textToImage 外部字体功能
// 演示使用外部字体文件和字体字节生成图片

(async function () {
    log.info("=== 开始测试 textToImage 外部字体功能 ===");

    // 测试1: 使用系统字体文件 (微软雅黑)
    var fontPath = "C:/Windows/Fonts/msyh.ttc";
    if (file.Exists(fontPath)) {
        var mat1 = textToImage.generateMatFromFontFile("微软雅黑", fontPath, 24, "#000000", "#FFFFFF");
        log.info("微软雅黑字体图片尺寸: " + mat1.Width + "x" + mat1.Height);
        mat1.Dispose();
    } else {
        log.info("未找到微软雅黑字体，跳过");
    }

    // 测试2: 使用系统字体 (黑体)
    var fontPath2 = "C:/Windows/Fonts/simhei.ttf";
    if (file.Exists(fontPath2)) {
        var mat2 = textToImage.generateMatFromFontFile("黑体字", fontPath2, 28, "#FF0000", "#FFFF00");
        log.info("黑体字图片尺寸: " + mat2.Width + "x" + mat2.Height);
        mat2.Dispose();
    }

    // 测试3: 使用项目内置字体(通过字体名称)
    var mat3 = textToImage.generateMat("HYW原神字体", "HYW", 24, "#505769", "#F5F6F7");
    log.info("内置HYW字体尺寸: " + mat3.Width + "x" + mat3.Height);
    mat3.Dispose();

    log.info("=== 测试完成 ===");
})();