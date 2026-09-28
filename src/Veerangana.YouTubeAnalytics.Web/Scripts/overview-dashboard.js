(function () {
    "use strict";

    var dataElement = document.getElementById("usage-trend-data");
    var canvas = document.getElementById("usage-trend-chart");

    if (!dataElement || !canvas || typeof Chart === "undefined") {
        return;
    }

    var points;
    try {
        points = JSON.parse(dataElement.textContent || "[]");
    } catch (error) {
        return;
    }

    new Chart(canvas.getContext("2d"), {
        type: "line",
        data: {
            labels: points.map(function (point) { return point.Label; }),
            datasets: [
                {
                    label: "Views",
                    data: points.map(function (point) { return point.Views; }),
                    borderColor: "#c34e67",
                    backgroundColor: "rgba(195, 78, 103, 0.08)",
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    lineTension: 0.28,
                    fill: true,
                    yAxisID: "views-axis"
                },
                {
                    label: "Watch hours",
                    data: points.map(function (point) { return point.WatchTimeHours; }),
                    borderColor: "#278d83",
                    backgroundColor: "rgba(39, 141, 131, 0)",
                    borderWidth: 2,
                    borderDash: [5, 4],
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    lineTension: 0.28,
                    fill: false,
                    yAxisID: "watch-axis"
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 850, easing: "easeOutQuart" },
            legend: { display: false },
            tooltips: {
                mode: "index",
                intersect: false,
                backgroundColor: "rgba(29, 31, 36, 0.92)",
                cornerRadius: 5,
                displayColors: true
            },
            hover: { mode: "index", intersect: false },
            scales: {
                xAxes: [{
                    gridLines: { display: false },
                    ticks: { maxTicksLimit: 8, fontColor: "#858891", fontSize: 10 }
                }],
                yAxes: [
                    {
                        id: "views-axis",
                        position: "left",
                        gridLines: { color: "rgba(118, 123, 134, 0.10)", drawBorder: false },
                        ticks: { beginAtZero: true, precision: 0, fontColor: "#858891", fontSize: 10 }
                    },
                    {
                        id: "watch-axis",
                        position: "right",
                        gridLines: { display: false },
                        ticks: { beginAtZero: true, fontColor: "#858891", fontSize: 10 }
                    }
                ]
            }
        }
    });
}());
