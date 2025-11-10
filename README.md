

# IEEE Miku Showcase
IEEE Showcase project

---

## The Project
This project's goal is to use position tracking and perspective projection to render scenes in Unity that update according to the viewer's position. This creates a 3D effect on the display, similar to a hologram. Specifically, the aim is to replicate the company Portalgraph's proprietary product.

### NOTE: I will add "deadlines" to each task. This basically means that by this deadline, if it isn't finished, I will begin working on that task myself because it's too close to the showcase day.

### Demo examples from Portalgaph:
[Displaying a 3D top-down map view on a single display](https://www.youtube.com/watch?v=qkbcDqxHRkM)

[Displaying an animated 3D character model in 3D space on two displays](https://www.youtube.com/watch?v=U7bCu9H4aw4)

## Current Progress

### Tracking
So right now this is the tracking we have. Note that it takes **cv2**, **mediapipe**, and **numpy** (unless that one already comes preinstalled). You can install those through:
```bash
pip install opencv-python
pip install mediapipe
````

*For mediapipe, you might have to install a model, I'm not sure. Let me know if it doesn't work.*

---

### Unity Files

As for the Unity files, apparently Git is bad with binary files. It just so happens that Unity is basically all binary files. I'm struggling with getting the push working with my Unity files (even with .gitignore unless I'm setting that up wrong), so those aren't there yet. If you want to work on Unity files, just let me know and I'll upload those individually.

It is almost finished though (it works at least, I just have to do some polishing. )  (:

---

## Current To-Do

### 1. Make facetracking distance tracking work better (11/15)

Currently, face tracking works pretty well using **PnP** (black magic included in the OpenCV library). However, the distance from the screen is currently scaled using the overall size of a bounding box around the face, which causes a lot of inaccuracies when the head isn't facing directly toward the camera and whatnot. It'd be great if that could be fixed.

I'm thinking maybe we can use the distance between certain landmarks, along with the height and width of the face, to determine the angle of the face.
Given how incredibly advanced OpenCV is though, there might be some method built in that just does that for you. I'm yet to find that though.

This'll take a decent lot of programming logic and figuring out the opencv library.

#### 1.a Determine the necessary camera quality for accurate-enough face tracking

Once the face tracking works, we need to find the necessary resolution of the camera so we can buy the cheapest webcam possible for the project. This would probably just be trial and error, conducted by passing a video in the facetracking script instead of a camera and seeing how accurate it is for different video resolutions.

---

### 2. Develop arUco tracking in 3D space (11/14)

arUco tracking would allow for us to test the Unity program using a camera with an arUco grid attached to it, and if worst comes to worst, we can forego face prediction and make them glasses/headband with an arUco grid on them. This would be an entirely new script. Don't worry about UDP protocal for now, I'll take care of that later once the script works.

---

### 3. Begin developing facetracking prediction (11/15)

Some kind of prediction system that would estimate where the face would go would be great. For whoever is developing this, you can assume that the facetracking performs flawlessly and gives you accurate and consistent positions. There may be a library out there to do it for you but I think this would be a good thing to develop.

This would be mostly math, research, and only take basic programming. The math would be a pain though.

---

### 4. Arduino code for potentially a rotating webcam (11/15)

Update: I 100% want to do this.
We'll have two servo motors that hook up to the webcam, allowing us to adjust the angle in all directions, not just side to side. This'll have to be written in Arduino code (C++ but wiht little Arduino quirks).

Whoever's working on this, assume that the face tracking code is perfectly accurate. 

---

### 5. Perspective projection in Unity

IT WORKS!! I'm going to brush things up, make the parameters more intuitive, and clean up the code, but it functions. I'm yet to test it on mulitple monitors, but it should theoretically work. I may have to do further tweaking to render multiple cameras at once, but I don't think that'll be the case.

---

### 6. (Potential) Sending Position data from one computer to another over a wired connection.

Since the webcam would be feeding in high resolution data at a high framerate (preferably), it might take too much processing power if it's running along with the UNity scene. I'm assuming wired protocal is extremely fast and doesn't introduce much delay, but I haven't worked with that before so I can't say. Wireless HTTP protocal or webhooks may work as well, but there will be an unavoidable delay.

---

## Shopping List

Budget: $50.00

Webcam: [Arducam 1080P Day and Night Vision USB Camera ($34.99)](https://www.amazon.com/Arducam-Computer-Automatic-Switching-All-Day/dp/B0829HZ3Q7?crid=21YI8U4UZ7DKL&dib=eyJ2IjoiMSJ9.z9-2jy-PjVuQhymGSMDVVCCdHB3hVFtRiLBGstOnNW-BW4qiXuCxR-DjEZ_F9EViUTIzh-lLB6scju5lDCtcnKpOhMXysZAXSWUeRA4S9ByjalhpunF24L6q1QEmcWLB-7RUySTfvqVgekBUc9tNByFtrkz6T-cNgg5KdKClQxY8wB62S_6RBoon--3ljxiuxlw7_cMvo_fZyTEB4dwPzuzWbGCR4038wrj1ORRusbQ.zdmMy4skk9rLNLlULifNQqZsGBgzlKQ_Fvdtb2NC_p8&dib_tag=se&keywords=arduino+webcam&qid=1762804508&sprefix=arduinowebcam%2Caps%2C103&sr=8-3)
This one has night vision, which I think would make sense for dark rooms and such. Weight is 0.07 kg.

Servo Motors (At least 2): [WWZMDiB SG90 Micro Servo Motor (3 Pcs) ($9.98)](https://www.amazon.com/WWZMDiB-SG90-Control-Servos-Arduino/dp/B0BKPL2Y21?crid=1J52HE9B003AC&dib=eyJ2IjoiMSJ9.zKdFX4LDfJj3RzL495S_2TuwhgVIi7j06WLFuMiyPHn0CEZoIaXiPe2kmULyG2IkY3NRlcRWWhkumo0MhHsMp988riHEPmZrULLqc9kThsclZ-6JV6wozo9HOOHIVF3MexYkBVivTZjZKRDFes6Ll8v0U-Dvn10WC7Rdmc4jYXYkQ8DAi1Tv85ehM7nTO5LCmaT7zJrtH47oDSQBCJdQys9He130ZRhRJZyZYY2-GNSgpQSmswk0udfb8iHRuRXI3abCue7Ia7PvvmR8h-jPMFt1ENlyB-OxKAM9gbugb0c.2r0NecAh9-KnOzb4hyKyJKsDwbzb-D03EGdsOuxyTuU&dib_tag=se&keywords=servo%2Bmotor&qid=1762807031&sprefix=servo%2Bmotor%2Caps%2C151&sr=8-6&th=1)
I think something like this might be good. Needs to be a servo motor (probably) and have no problem moving around the weight of the camera (0.07 kg) and the weight of another servo motor. I have to double check if this one has that. Might be best to consult Patrick on this one.


