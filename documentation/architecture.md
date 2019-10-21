# architecture

## phone app features

- Create audio stream
- Subscribe to events
- Display audio stream
- Display timer
- Display status

## sample design

The android AudioRecord class supports start, stop, and release functions. To build this into an MVU application, the audio service must be controlled, but also be able to provide updates to the UI.

- Start recording to a buffer
- Periodically (every 100 ms?) read the buffer and update the UI
- Stop recording to deactivate
- Release recording when exiting