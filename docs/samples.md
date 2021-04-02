

## Welcome to the _Machina Common Language_! 

Here, you can find a few examples of simple Machina programs for an industrial robotic arm. Unless otherwise specified, these examples assume your are manipulating a Universal Robots UR10 model. So if you are working with a different brand/model, make sure you adjust the values to the range of motion of your robot!

Feel free to use the Bridge to connect to a robot, virtual or physical, and copy-paste these programs on the Bridge terminal!

A simple program that traces a YZ 100x100 square in the air:

```csharp
Move(0, 100, 0);
Move(0, 0, 100);
Move(0, -100, 0);
Move(0, 0, -100);
```

You can make the robot move faster by setting a speed value before the motion actions. You can also "round" the motion between actions by choosing a high precision value:

```csharp
SpeedTo(50);      // set the speed in mm/s
PrecisionTo(25);  // blend motion with a 25 mm radius
Move(0, 100, 0);
Move(0, 0, 100);
Move(0, -100, 0);
Move(0, 0, -100);
```

A program that moves to an absolute location, and traces a vertical line:

```csharp
// Note that MoveTo(x, y, z) means absolute coordinates, 
// whereas Move(x, y, z) means relative coordinates. 
// In general, Machina actions with the "To" suffix are absolute, 
// whereas without it they are relative. 

MoveTo(500, 150, 600);
Wait(2000);  // wait 2 seconds before next action
Move(0, 0, 100);
Wait(2000); 
MoveTo(690, -177, 676);  // this is close to home for UR10
```

After moving to a target, we can change the orientation of the flange by rotating the TCP:

```csharp
// Rotate(x, y, z, angle) is relative to where the robot is, 
// and uses a rotation vector and the angle in degrees 

MoveTo(500, 150, 600);
Rotate(0, 1, 0, -90);
Wait(2000);
Move(0, 0, 100);
Rotate(0, 1, 0, 90);
MoveTo(690, -177, 676);
```

And if we want to move and rotate with the same motion, we can use the combined `TransformTo` action to specify location, and the direction of the main X and Y axes of the TCP:

```csharp
// TransformTo(xpos, ypos, zpos, x0, x1, x2, y0, y1, y2)
// completely reorients the TCP to target position, 
// and with the X and Y axes of the TCP pointing 
// according to the input vectors in world coordinates.
  
// X axis pointing at world -Z, and Y axis towards world +Y
TransformTo(500, 150, 600, 0, 0, -1, 0, 1, 0);

Wait(2000);
Move(0, 0, 100);
Wait(2000);

// X axis pointing at world -X, and Y axis towards world +Y
TransformTo(690, -177, 676, -1, 0, 0, 0, 1, 0);
```

We can change the robot speed at different points during the program:

```csharp
SpeedTo(100);
TransformTo(500, 150, 600, 0, 0, -1, 0, 1, 0);
Wait(2000);

SpeedTo(20);
Move(0, 0, 100);
Wait(2000);

SpeedTo(100);
TransformTo(690, -177, 676, -1, 0, 0, 0, 1, 0);
```
