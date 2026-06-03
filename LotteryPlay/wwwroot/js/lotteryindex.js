// wwwroot/js/LotteryIndex.js
var currLotteryId = 0;
var currPlayId = 0;
var currPlayName = ""; //当前玩法名称
var currPeriod = "";
var canBet = false;
var selectNumsArr = []; //存选中号码数组
var selectNums = "";    //最终拼接字符串（传给后端）
var playList = [];
var selectStartPeriod = ""; //选中的起始期号

//勾选追号开关
$("#chkTrace").change(function () {
    if ($(this).is(":checked")) {
        $("#traceBox,#traceInputBox").show();
        $("#btnTrace").show();
        $("#btnBet").hide();
        LoadHistoryPeriod();
    } else {
        $("#traceBox,#traceInputBox").hide();
        $("#btnTrace").hide();
        $("#btnBet").show();
        selectStartPeriod = "";
    }
})

//【只保留1个加载历史：单选框，后端返回字段统一 PeriodNo】
function LoadHistoryPeriod() {
    $.get("/Index?handler=GetHistoryPeriod", { lid: currLotteryId }, function (res) {
        let html = "";
        $.each(res, function (i, item) {
            html += `<label style="margin-right:12px">
                <input type="radio" name="perRadio" value="${item.periodNo}">${item.periodNo}
            </label>`
        })
        $("#historyPeriodBox").html(html);
        //单选赋值起始期
        $("input[name=perRadio]").off("change").on("change", function () {
            selectStartPeriod = $(this).val();
        })
    }, "json")
}

//【只保留1个追号提交】
$("#btnTrace").click(function () {
    if (!currPlayId || currPlayId <= 0) { alert("请选择玩法"); return; }
    if (!selectNums || selectNums == "") { alert("请选择号码"); return; }
    if (!selectStartPeriod) { alert("请勾选起始历史期"); return; }
    let totalNum = parseInt($("#traceTotal").val());
    if (totalNum < 1) { alert("追号期数不能小于1"); return; }

    let send = {
        lid: currLotteryId,
        pid: currPlayId,
        startPer: selectStartPeriod,
        betNum: selectNums,
        mul: $("#multiple").val(),
        traceCount: totalNum
    }
    $.post("/Index?handler=AddHistoryTrace", send, function (res) {
        alert(res.msg);
        if (res.code == 1) {
            $(".num-item").removeClass("selected");
            selectNums = "";
            $("input[name=perRadio]").prop("checked", false);
            selectStartPeriod = "";
        }
    }, "json")
})

//渲染选号+全大小单双清
function renderNumPanel(playName) {
    var html = "";
    if (!playName || playName == undefined) {
        $("#numWrap").html("<div>暂无玩法</div>");
        selectNums = "";
        return;
    }
    if (playName.indexOf("直选") > -1) {
        var posArr = ["万位", "千位", "百位", "十位", "个位"];
        for (var p = 0; p < 5; p++) {
            html += '<div class="num-row">';
            html += '<span class="pos-label">' + posArr[p] + '</span>';
            for (var i = 0; i <= 9; i++) {
                html += '<span class="num-item" data-pos="' + p + '" data-num="' + i + '">' + i + '</span>';
            }
            html += '<div class="pos-btn-group">';
            html += '<button class="pos-btn" data-action="all" data-pos="' + p + '">全</button>';
            html += '<button class="pos-btn" data-action="big" data-pos="' + p + '">大</button>';
            html += '<button class="pos-btn" data-action="small" data-pos="' + p + '">小</button>';
            html += '<button class="pos-btn" data-action="odd" data-pos="' + p + '">单</button>';
            html += '<button class="pos-btn" data-action="even" data-pos="' + p + '">双</button>';
            html += '<button class="pos-btn" data-action="clear" data-pos="' + p + '">清</button>';
            html += '</div></div>';
        }
    } else {
        html += '<div class="num-row">';
        html += '<span class="pos-label">号码</span>';
        for (var i = 0; i <= 9; i++) {
            html += '<span class="num-item" data-pos="99" data-num="' + i + '">' + i + '</span>';
        }
        html += '<div class="pos-btn-group">';
        html += '<button class="pos-btn" data-action="all" data-pos="99">全</button>';
        html += '<button class="pos-btn" data-action="big" data-pos="99">大</button>';
        html += '<button class="pos-btn" data-action="small" data-pos="99">小</button>';
        html += '<button class="pos-btn" data-action="odd" data-pos="99">单</button>';
        html += '<button class="pos-btn" data-action="even" data-pos="99">双</button>';
        html += '<button class="pos-btn" data-action="clear" data-pos="99">清</button>';
        html += '</div></div>';
    }
    $("#numWrap").html(html);
    selectNums = "";
}

function calcTotalMoney() {
    var multiple = parseInt($("#multiple").val()) || 1;
    var total = selectNums.length * multiple;
}

//加载彩种
function loadLottery() {
    $.get("/Index?handler=GetLotteryList", function (res) {
        console.log("彩种接口返回:", res);
        if (res && res.code === 1 && res.data && res.data.length > 0) {
            var html = "";
            for (var i = 0; i < res.data.length; i++) {
                var item = res.data[i];
                html += '<div class="lottery-nav-item" data-id="' + item.id + '">' + item.lotteryName + '</div>';
            }
            $("#lotteryNav").html(html);

            currLotteryId = res.data[0].id;
            $("#lotteryNav .lottery-nav-item:first").addClass("active");
            $("#lotteryTitle").text(res.data[0].lotteryName);

            loadPlay(currLotteryId);
            getCurrentPeriod(currLotteryId);
            getHistory(currLotteryId);
        }
    }, "json");
}

//加载玩法
function loadPlay(lotteryId) {
    $.get("/Index?handler=GetPlayList&lotteryId=" + lotteryId, function (res) {
        console.log("玩法接口返回:", res);
        playList = res.data || [];
        var playHtml = "";
        var bonusHtml = "";

        if (playList.length === 0) {
            playHtml = "<span style='color:#666;'>暂无可用玩法</span>";
            bonusHtml = "<p>暂无奖金配置</p>";
        } else {
            for (var i = 0; i < playList.length; i++) {
                var item = playList[i];
                var active = i === 0 ? "active" : "";
                playHtml += '<button class="' + active + '" data-playid="' + item.id + '">' + item.playName + '</button>';
                if (item.BonusAmount > 0) {
                    bonusHtml += '<p>' + item.playName + '：' + item.bonusAmount + '元</p>';
                }
            }
            currPlayId = playList[0].Id;
            currPlayName = playList[0].PlayName;
            renderNumPanel(currPlayName);
        }

        $("#playGroup").html(playHtml);
        $("#bonusList").html(bonusHtml);

        selectNums = [];
        $(".num-item").removeClass("selected");
    }, "json");
}
//加载历史
function renderHistory(list) {
    var html = "";
    if (!list || list.length === 0) {
        html = '<tr><td colspan="3" class="empty-tip">暂无开奖数据</td></tr>';
    } else {
        for (var i = 0; i < list.length; i++) {
            var item = list[i];
            html += '<tr><td>' + item.period + '</td><td>' + item.openTime + '</td><td>' + item.openNumber + '</td></tr>';
        }
    }
    $("#historyTable tbody").html(html);
}

function getHistory(lotId) {
    $.get("/Index?handler=LotteryHistory&lotId=" + lotId, function (res) {
        if (res.code === 1) renderHistory(res.data);
    }, "json");
}

function getCurrentPeriod(lotId) {
    $.get("/Index?handler=CurrentPeriod&lotId=" + lotId, function (res) {
        if (res.code === 1) {
            currPeriod = res.period;
            $("#currPeriod").text(res.period);
            $("#countTime").text(res.countTime);
            $("#betStatus").text(res.canBet ? "正常投注" : "已截止");
            canBet = res.canBet;
        }
    }, "json");
}

$(function () {
    loadLottery();
    setInterval(function () {
        getCurrentPeriod(currLotteryId);
        getHistory(currLotteryId);
    }, 2000);

    // 彩种切换
    $(document).on("click", ".lottery-nav-item", function () {
        $(".lottery-nav-item").removeClass("active");
        $(this).addClass("active");
        currLotteryId = parseInt($(this).data("id"));
        $("#lotteryTitle").text($(this).text());
        loadPlay(currLotteryId);
        getCurrentPeriod(currLotteryId);
        getHistory(currLotteryId);
    });

    // 玩法切换
    $(document).on("click", ".play-type-group button", function () {
        $(".play-type-group button").removeClass("active");
        $(this).addClass("active");
        currPlayId = parseInt($(this).data("playid"));
        currPlayName = $(this).text();
        renderNumPanel(currPlayName);
    });

    //快捷选号按钮
    $(document).on("click", ".pos-btn", function () {
        var pos = $(this).data("pos");
        var act = $(this).data("action");
        var items = $(".num-item[data-pos='" + pos + "']");

        if (act === "clear") {
            items.removeClass("selected");
        } else {
            items.removeClass("selected");
            switch (act) {
                case "all": items.addClass("selected"); break;
                case "big": items.filter(function () { return parseInt($(this).data("num")) >= 5; }).addClass("selected"); break;
                case "small": items.filter(function () { return parseInt($(this).data("num")) <= 4; }).addClass("selected"); break;
                case "odd": items.filter(function () { return parseInt($(this).data("num")) % 2 === 1; }).addClass("selected"); break;
                case "even": items.filter(function () { return parseInt($(this).data("num")) % 2 === 0; }).addClass("selected"); break;
            }
        }
        collectSelectedNum();
    });

    //单点数字选中
    $(document).on("click", ".num-item", function () {
        $(this).toggleClass("selected");
        collectSelectedNum();
    });
    $("#multiple").change(calcTotalMoney);

    // 普通投注按钮
    $("#btnBet").click(function () {
        if (!canBet) {
            alert("投注已截止！");
            return;
        }
        if (selectNums.length === 0) {
            alert("请选择号码！");
            return;
        }
        if (!currPeriod) {
            alert("暂无期号！");
            return;
        }
        if (currPlayId === 0) {
            alert("请选择玩法！");
            return;
        }

        var data = {
            lotteryId: currLotteryId,
            playId: currPlayId,
            period: currPeriod,
            betNum: selectNums,
            multiple: $("#multiple").val()
        };

        $.post("/Index?handler=Bet", data, function (res) {
            alert(res.msg);
            if (res.code === 1) location.reload();
        }, "json");
    });

    function collectSelectedNum() {
        selectNumsArr = [];
        if (currPlayName.indexOf("直选") > -1) {
            var arr = ["", "", "", "", ""];
            $(".num-item.selected").each(function () {
                var p = parseInt($(this).data("pos"));
                var n = $(this).data("num");
                arr[p] += n;
            });
            selectNums = arr.join("|");
        } else {
            $(".num-item.selected").each(function () {
                selectNumsArr.push($(this).data("num"));
            });
            selectNums = selectNumsArr.join(",");
        }
    }
});