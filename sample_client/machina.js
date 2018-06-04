
// Quick Machina-like API to connect to MachinaBridge
class Robot {
    constructor(socket) {
        this.socket = socket;
    }

    Move(x, y) {
        this.socket.send("Move(" + x + "," + y + ",0);");
    }

    Move(x, y, z) {
        this.socket.send("Move(" + x + "," + y + "," + z + ");");
    }

    MoveTo(x, y, z) {
        this.socket.send("MoveTo(" + x + "," + y + "," + z + ");");
    }

    TransformTo(x, y, z, x0, x1, x2, y0, y1, y2) {
        this.socket.send("TransformTo(" + x + "," + y + "," + z + "," +
            x0 + "," + x1 + "," + x2 + "," +
            y0 + "," + y1 + "," + y2 + ");");
    }

    Rotate(x, y, z, angle) {
        this.socket.send("Rotate(" + x + "," + y + "," + z + "," + angle + ");");
    }

    RotateTo(x0, x1, x2, y0, y1, y2) {
        this.socket.send("RotateTo(" + x0 + "," + x1 + "," + x2 + "," +
            y0 + "," + y1 + "," + y2 + ");");
    }

    Axes(j1, j2, j3, j4, j5, j6) {
        this.socket.send("Axes(" + j1 + "," + j2 + "," + j3 + "," + j4 + "," + j5 + "," + j6 + ");");
    }

    AxesTo(j1, j2, j3, j4, j5, j6) {
        this.socket.send("AxesTo(" + j1 + "," + j2 + "," + j3 + "," + j4 + "," + j5 + "," + j6 + ");");
    }

    Speed(speed) {
        this.socket.send("Speed(" + speed + ");");
    }

    SpeedTo(speed) {
        this.socket.send("SpeedTo(" + speed + ");");
    }

    Precision(precision) {
        this.socket.send("Precision(" + precision + ");");
    }

    PrecisionTo(precision) {
        this.socket.send("PrecisionTo(" + precision + ");");
    }

    MotionMode(mode) {
        this.socket.send('MotionMode("' + mode + '");');
    }

    Message(msg) {
        this.socket.send('Message("' + msg + '");');
    }

    Wait(millis) {
        this.socket.send("Wait(" + millis + ");");
    }

    PushSettings() {
        this.socket.send("PushSettings();");
    }

    PopSettings() {
        this.socket.send("PopSettings();");
    }

    Tool(name, x, y, z, x0, x1, x2, y0, y1, y2, weightkg, gx, gy, gz) {
        this.socket.send(`new Tool("${name}",${x},${y},${z},${x0},${x1},${x2},${y0},${y1},${y2},${weightkg},${gx},${gy},${gz});`);
    }

    Attach(toolName) {
        this.socket.send(`Attach("${toolName}");`);
    }

    Detach() {
        this.socket.send("Detach();");
    }
}

module.exports.Robot = Robot;