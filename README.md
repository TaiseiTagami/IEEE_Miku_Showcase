

# IEEE Miku Showcase
IEEE Showcase project

---

## The Project
This project's goal is to use position tracking and perspective projection to render scenes in Unity that update according to the viewer's position. This creates a 3D effect on the display, similar to a hologram. Specifically, the aim is to replicate the company Portalgraph's proprietary product.

### Demo examples from Portalgaph:
Displaying a 3D top-down map view on a single display: [text](https://www.youtube.com/watch?v=qkbcDqxHRkM)
Displaying an animated 3D character model in 3D space on two displays: [text](https://www.youtube.com/watch?v=U7bCu9H4aw4)

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

---

# Current To-Do

### 1. Make facetracking distance tracking work better

Currently, face tracking works pretty well using **PnP** (black magic included in the OpenCV library). However, the distance from the screen is currently scaled using the overall size of a bounding box around the face, which causes a lot of inaccuracies when the head isn't facing directly toward the camera and whatnot. It'd be great if that could be fixed.

I'm thinking maybe we can use the distance between certain landmarks, along with the height and width of the face, to determine the angle of the face.
Given how incredibly advanced OpenCV is though, there might be some method built in that just does that for you. I'm yet to find that though.

This'll take a decent lot of programming logic and figuring out the opencv library.

#### 1.a Determine the necessary camera quality for accurate-enough face tracking

Once the face tracking works, we need to find the necessary resolution of the camera so we can buy the cheapest webcam possible for the project. This would probably just be trial and error, conducted by passing a video in the facetracking script instead of a camera and seeing how accurate it is for different video resolutions.

---

### 2. Begin developing facetracking prediction

Some kind of prediction system that would estimate where the face would go would be great. For whoever is developing this, you can assume that the facetracking performs flawlessly and gives you accurate and consistent positions. There may be a library out there to do it for you but I think this would be a good thing to develop.

This would be mostly math, research, and only take basic programming. The math would be a pain though.

---

### 3. Arduino code for potentially a rotating webcam

This might mess up a lot of the code written before and we might not have enough time for this, so this is the lowest priority.

---

### 4. Perspective projection in Unity

I'm trying to use perspective projection formulas using matrices to try to project what the viewer should be seeing from their position in real life in the Unity scene. Currently, I have the live positioning in reference to the camera, viewer's line of sight and viewing angles, and simulated monitor objects equivalent to the real-world monitors working. Some of the projection mapping is working, but tbh I don't fully understand how it's working and it's still wonky. I haven't really had the chance to test it in the real world either so it'll prob require a lot of calibration parameters that I haven't implemented. Hopefully I can get my hands on some monitors soon.

---

### 5. Buying an Arduino, servo motor, and webcam

Apparently the form for requesting funds was due like last week ): I'll buy an Arduino, servo motor, and webcam maybe over the weekend. Even if we don't use it, it'd be nice to have for future stuff.


