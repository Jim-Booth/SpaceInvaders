// Canvas and audio interop for Space Invaders emulator

window.gameInterop = {
    canvas: null,
    ctx: null,
    imageData: null,
    sounds: {},
    dotNetHelper: null,
    
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
    },

    // Check if the device is mobile / touch-capable
    isMobile: function() {
        return /Android|iPhone|iPad|iPod|webOS|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)
            || (navigator.maxTouchPoints && navigator.maxTouchPoints > 1);
    },

    // Initialize on-screen touch controls for mobile devices
    initializeTouchControls: function(dotNetHelper) {
        this.dotNetHelper = dotNetHelper;

        if (!this.isMobile()) return;

        if (!this.canvas) {
            console.error('Canvas not found - cannot create touch controls');
            return;
        }

        // Create touch controls container
        const controlsDiv = document.createElement('div');
        controlsDiv.id = 'touch-controls';
        controlsDiv.innerHTML = `
            <div class="touch-row">
                <div class="touch-slider" id="touch-slider">
                    <div class="slider-zone slider-zone-left">&#9664;</div>
                    <div class="slider-zone slider-zone-center">&#x2022;</div>
                    <div class="slider-zone slider-zone-right">&#9654;</div>
                    <div class="slider-thumb" id="slider-thumb"></div>
                </div>
                <button class="touch-btn touch-fire" id="btn-fire">FIRE</button>
            </div>
            <div class="touch-row">
                <button class="touch-btn touch-action" id="btn-1p">1P</button>
                <button class="touch-btn touch-action" id="btn-coin">COIN</button>
                <button class="touch-btn touch-action" id="btn-2p">2P</button>
            </div>
        `;

        // Insert after the canvas
        this.canvas.parentNode.insertBefore(controlsDiv, this.canvas.nextSibling);

        // --- Directional slider logic ---
        const slider = document.getElementById('touch-slider');
        const thumb = document.getElementById('slider-thumb');
        let sliderDirection = null; // null, 'ArrowLeft', or 'ArrowRight'

        const updateSliderDirection = (clientX) => {
            const rect = slider.getBoundingClientRect();
            const relX = clientX - rect.left;
            const pct = relX / rect.width;

            // Move the thumb visual
            const clampedPct = Math.max(0, Math.min(1, pct));
            thumb.style.left = (clampedPct * 100) + '%';

            // Determine zone: left / narrow center deadzone / right
            let newDir = null;
            if (pct < 0.47) {
                newDir = 'ArrowLeft';
                slider.className = 'touch-slider active-left';
            } else if (pct > 0.53) {
                newDir = 'ArrowRight';
                slider.className = 'touch-slider active-right';
            } else {
                newDir = null;
                slider.className = 'touch-slider active-center';
            }

            // Only send events when direction changes
            if (newDir !== sliderDirection) {
                // Release previous direction
                if (sliderDirection && this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnTouchKeyUp', sliderDirection);
                }
                // Press new direction
                if (newDir && this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnTouchKeyDown', newDir);
                }
                sliderDirection = newDir;
            }
        };

        const resetSlider = () => {
            if (sliderDirection && this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnTouchKeyUp', sliderDirection);
            }
            sliderDirection = null;
            slider.className = 'touch-slider';
            thumb.style.left = '50%';
        };

        // Touch events for slider
        slider.addEventListener('touchstart', (e) => {
            e.preventDefault();
            updateSliderDirection(e.touches[0].clientX);
        }, { passive: false });

        slider.addEventListener('touchmove', (e) => {
            e.preventDefault();
            updateSliderDirection(e.touches[0].clientX);
        }, { passive: false });

        slider.addEventListener('touchend', (e) => {
            e.preventDefault();
            resetSlider();
        }, { passive: false });

        slider.addEventListener('touchcancel', (e) => {
            e.preventDefault();
            resetSlider();
        }, { passive: false });

        // Mouse events for slider (desktop testing)
        let sliderMouseDown = false;
        slider.addEventListener('mousedown', (e) => {
            e.preventDefault();
            sliderMouseDown = true;
            updateSliderDirection(e.clientX);
        });
        document.addEventListener('mousemove', (e) => {
            if (sliderMouseDown) {
                updateSliderDirection(e.clientX);
            }
        });
        document.addEventListener('mouseup', () => {
            if (sliderMouseDown) {
                sliderMouseDown = false;
                resetSlider();
            }
        });

        // --- Simple button bindings (fire, coin, 1p, 2p) ---
        const buttonKeyMap = {
            'btn-fire':  ' ',
            'btn-coin':  'c',
            'btn-1p':    '1',
            'btn-2p':    '2'
        };

        for (const [btnId, key] of Object.entries(buttonKeyMap)) {
            const btn = document.getElementById(btnId);

            const startHandler = (e) => {
                e.preventDefault();
                btn.classList.add('active');
                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnTouchKeyDown', key);
                }
            };
            const endHandler = (e) => {
                e.preventDefault();
                btn.classList.remove('active');
                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnTouchKeyUp', key);
                }
            };

            // Touch events (primary for mobile)
            btn.addEventListener('touchstart', startHandler, { passive: false });
            btn.addEventListener('touchend', endHandler, { passive: false });
            btn.addEventListener('touchcancel', endHandler, { passive: false });

            // Mouse events (fallback for desktop testing)
            btn.addEventListener('mousedown', startHandler);
            btn.addEventListener('mouseup', endHandler);
            btn.addEventListener('mouseleave', endHandler);
        }

        console.log('Touch controls initialized');
    }
};
