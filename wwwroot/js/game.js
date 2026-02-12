// Canvas and audio interop for Space Invaders emulator

window.gameInterop = {
    canvas: null,
    ctx: null,
    imageData: null,
    sounds: {},
    
    // Initialize the canvas
    initialize: function(canvasId, width, height) {
        console.log('Initializing canvas:', canvasId, width, 'x', height);
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) {
            console.error('Canvas not found:', canvasId);
            return false;
        }
        this.canvas.width = width;
        this.canvas.height = height;
        this.ctx = this.canvas.getContext('2d');
        this.imageData = this.ctx.createImageData(width, height);
        
        // Fill with black initially
        this.ctx.fillStyle = '#000';
        this.ctx.fillRect(0, 0, width, height);
        
        console.log('Canvas initialized successfully');
        return true;
    },
    
    // Draw a frame from RGBA pixel data
    drawFrame: function(pixelData) {
        if (!this.ctx || !this.imageData) {
            console.error('Canvas not initialized');
            return;
        }
        this.imageData.data.set(new Uint8ClampedArray(pixelData));
        this.ctx.putImageData(this.imageData, 0, 0);
    },
    
    // Load a sound file
    loadSound: function(id, url) {
        const audio = new Audio(url);
        audio.load();
        this.sounds[id] = audio;
        console.log('Sound loaded:', id);
    },
    
    // Play a sound by ID
    playSound: function(id) {
        const sound = this.sounds[id];
        if (sound) {
            // Clone the audio for overlapping sounds
            const clone = sound.cloneNode();
            clone.volume = 0.5;
            clone.play().catch(e => {
                // Ignore autoplay errors - user hasn't interacted yet
            });
        }
    }
};
