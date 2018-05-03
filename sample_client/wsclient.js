const WebSocket = require('ws');
const ws = new WebSocket('ws://127.0.0.1:6999/Bridge');
// const generateName = require('sillyname');
// const name = generateName();


ws.on('open', function open() {
    //   ws.send('something');
});

ws.on('message', function incoming(data) {
    let json;
    try {
        json = JSON.parse(data);
        console.log(json);
    } catch (e) {
        console.log(data);
    }
});


// https://stackoverflow.com/a/24466103/1934487
var readline = require('readline');
var log = console.log;
var rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});


var recursiveAsyncReadLine = function () {
    rl.question('Type here: ', answer => {

        if (answer == "exit") {
            process.exit();

        } else if (answer == "home") {
            ws.send("SpeedTo(200);");
            ws.send("AxesTo(0,0,0,0,90,0);");

        } else if (answer == "square") {
            let actions = [
                "SpeedTo(100);",
                "TransformTo(400, 400, 400, -1, 0, 0, 0, 1, 0);",
                "Move(50, 0, 0);",
                "Move(0, 50, 0);",
                "Move(-50, 0, 0);",
                "Move(0, -50, 0);",
                "Move(0, 0, 50);",
                "Move(50, 0, 0);",
                "Move(0, 50, 0);",
                "Move(-50, 0, 0);",
                "Move(0, -50, 0);",
                "Move(0, 0, 50);",
                "Move(50, 0, 0);",
                "Move(0, 50, 0);",
                "Move(-50, 0, 0);",
                "Move(0, -50, 0);",
                "Move(0, 0, 50);",
                "Move(50, 0, 0);",
                "Move(0, 50, 0);",
                "Move(-50, 0, 0);",
                "Move(0, -50, 0);",
                "Move(0, 0, 50);"
            ];

            actions.forEach((a) => ws.send(a));

        } else {
            ws.send(answer);
        }

        // log(`Got it! Doing: "${answer}"`);
        recursiveAsyncReadLine();
    });
};

recursiveAsyncReadLine(); //we have to actually start our recursion somehow