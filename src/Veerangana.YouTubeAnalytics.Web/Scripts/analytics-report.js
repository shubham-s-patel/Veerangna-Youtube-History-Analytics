(function () {
    "use strict";

    var dataElement = document.getElementById("analytics-report-data");
    var canvas = document.getElementById("analytics-report-chart");
    if (!dataElement || !canvas || typeof Chart === "undefined") {
        return;
    }

    var report;
    try {
        report = JSON.parse(dataElement.textContent || "{}");
    } catch (error) {
        return;
    }

    var points = report.points || [];
    var isBar = report.type === "bar";
    var commonDataset = {
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 4,
        lineTension: 0.28
    };

    new Chart(canvas.getContext("2d"), {
        type: isBar ? "bar" : "line",
        data: {
            labels: points.map(function (point) { return point.Label; }),
            datasets: [
                Object.assign({}, commonDataset, {
                    label: report.primaryLabel,
                    data: points.map(function (point) { return point.PrimaryValue; }),
                    borderColor: "#c34e67",
                    backgroundColor: isBar ? "rgba(195, 78, 103, 0.74)" : "rgba(195, 78, 103, 0.08)",
                    fill: !isBar,
                    yAxisID: "primary-axis"
                }),
                Object.assign({}, commonDataset, {
                    label: report.secondaryLabel,
                    data: points.map(function (point) { return point.SecondaryValue; }),
                    borderColor: "#278d83",
                    backgroundColor: isBar ? "rgba(39, 141, 131, 0.58)" : "rgba(39, 141, 131, 0)",
                    borderDash: isBar ? [] : [5, 4],
                    fill: false,
                    yAxisID: "secondary-axis"
                })
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 800, easing: "easeOutQuart" },
            legend: { display: false },
            tooltips: {
                mode: "index",
                intersect: false,
                backgroundColor: "rgba(29, 31, 36, 0.92)",
                cornerRadius: 5
            },
            hover: { mode: "index", intersect: false },
            scales: {
                xAxes: [{
                    gridLines: { display: false },
                    ticks: { maxTicksLimit: isBar ? 10 : 8, fontColor: "#858891", fontSize: 10 }
                }],
                yAxes: [
                    {
                        id: "primary-axis",
                        position: "left",
                        gridLines: { color: "rgba(118, 123, 134, 0.10)", drawBorder: false },
                        ticks: { beginAtZero: true, precision: 0, fontColor: "#858891", fontSize: 10 }
                    },
                    {
                        id: "secondary-axis",
                        position: "right",
                        gridLines: { display: false },
                        ticks: { beginAtZero: true, fontColor: "#858891", fontSize: 10 }
                    }
                ]
            }
        }
    });
}());
