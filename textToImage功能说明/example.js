// 测试 textToImage.generateMat 接口
// 根据提供的姓名生成图片并在内存中匹配，将人员放入房间

var config = {
    // 要放入房间的玩家名字列表
    playerNames: ["空", "荧", "旅行者", "派蒙"],
    // 识别区域 (YUI界面玩家申请区域)
    recognizeRegion: { x: 664, y: 481, w: 691, h: 107 },
    // 匹配阈值 (0.0-1.0，默认0.8)
    matchThreshold: 0.95,
    // 超时时间(毫秒)
    timeout: 60000,
    // 识别间隔(毫秒)
    interval: 500
};

// YUI界面模板
var yUIRo = RecognitionObject.TemplateMatch(file.ReadImageMatSync("assets/RecognitionObject/yUI.png"));
// 允许按钮模板
var allowEnterRo = RecognitionObject.TemplateMatch(file.ReadImageMatSync("assets/RecognitionObject/allowEnter.png"));

// 存储生成的玩家名字Mat
var playerMats = {};

(async function () {
    log.info("开始测试 textToImage.generateMat 接口");

    // 1. 为每个玩家名字生成Mat
    for (var i = 0; i < config.playerNames.length; i++) {
        var name = config.playerNames[i];
        var mat = textToImage.generateMat(name, "HYW", 24, "#505769", "#F5F6F7");
        playerMats[name] = mat;
        log.info(`为玩家[${name}]生成Mat成功，尺寸: ${mat.Width}x${mat.Height}`);
    }

    log.info("开始等待玩家进入房间...");

    // 2. 等待YUI界面并识别玩家
    var startTime = new Date();
    var foundPlayer = null;

    while (new Date() - startTime < config.timeout) {
        // 检查YUI界面是否出现
        var gameRegion = captureGameRegion();
        var yuiRes = gameRegion.Find(yUIRo);
        gameRegion.Dispose();

        if (yuiRes.isExist()) {
            log.info("检测到YUI界面，开始识别玩家...");

            // 在识别区域内查找匹配的玩家名字
            var found = await recognizePlayer();
            if (found) {
                log.info(`识别到玩家: ${found}，点击允许按钮`);
                foundPlayer = found;

                // 点击允许按钮
                if (await findAndClick(allowEnterRo, true, 1000)) {
                    log.info(`已点击允许按钮，玩家[${found}]已放入房间`);
                    await sleep(1000);

                    // 可选：按ESC关闭YUI界面
                    keyPress("VK_ESCAPE");
                    await sleep(500);
                }
                break;
            }
        }

        await sleep(config.interval);
    }

    if (!foundPlayer) {
        log.info("超时未识别到白名单玩家");
    }

    log.info("测试完成");

    // 清理Mat资源
    for (var name in playerMats) {
        if (playerMats[name]) {
            playerMats[name].Dispose();
        }
    }

    // 辅助函数：在YUI界面识别白名单玩家
    async function recognizePlayer() {
        var reg = config.recognizeRegion;

        for (var i = 0; i < config.playerNames.length; i++) {
            var name = config.playerNames[i];
            var playerMat = playerMats[name];
            if (!playerMat || playerMat.Empty()) continue;

            // 创建识别对象
            var playerRo = RecognitionObject.TemplateMatch(playerMat, reg.x, reg.y, reg.w, reg.h);
            playerRo.Threshold = config.matchThreshold || 0.9;
            playerRo.InitTemplate();

            var checkRegion = captureGameRegion();
            var res = checkRegion.Find(playerRo);
            checkRegion.Dispose();

            if (res.isExist()) {
                log.info(`在区域[${reg.x},${reg.y},${reg.w},${reg.h}]匹配到玩家: ${name}`);
                return name;
            }
        }

        // 如果模板匹配失败，尝试OCR识别
        var ocrRegion = captureGameRegion();
        var ocrResults = ocrRegion.Find(RecognitionObject.ocr(reg.x, reg.y, reg.w, reg.h));
        ocrRegion.Dispose();

        if (ocrResults.count > 0) {
            for (var j = 0; j < ocrResults.count; j++) {
                var ocrText = ocrResults[j].text.trim();
                log.info(`OCR识别到文字: ${ocrText}`);

                // 检查是否在白名单中
                for (var k = 0; k < config.playerNames.length; k++) {
                    var whiteName = config.playerNames[k];
                    if (ocrText === whiteName || ocrText.includes(whiteName)) {
                        return whiteName;
                    }
                }
            }
        }

        return null;
    }

    // 辅助函数：查找并点击
    async function findAndClick(target, doClick, timeout) {
        doClick = doClick !== false;
        timeout = timeout || 3000;

        var start = new Date();
        while (new Date() - start < timeout) {
            var gameRegion = captureGameRegion();
            var res = gameRegion.Find(target);
            gameRegion.Dispose();

            if (res.isExist()) {
                if (doClick) {
                    res.Click();
                }
                return true;
            }
            await sleep(100);
        }
        return false;
    }

})();