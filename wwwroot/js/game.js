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
        let lastTurnX = null;       // reference point for detecting direction change
        let peakX = null;           // furthest X reached in current direction
        let sliderOriginX = null;   // initial touch X for visual thumb positioning
        const SLIDER_DEADZONE = 3;  // pixels of movement before direction triggers

        const updateSliderDirection = (clientX) => {
            if (lastTurnX === null) return;

            const rect = slider.getBoundingClientRect();

            // Move the thumb visual relative to center based on total delta from origin
            const totalDelta = clientX - sliderOriginX;
            const centerPct = 50;
            const deltaPct = (totalDelta / rect.width) * 100;
            const clampedPct = Math.max(0, Math.min(100, centerPct + deltaPct));
            thumb.style.left = clampedPct + '%';

            // Calculate delta from the last turning point
            const delta = clientX - lastTurnX;

            // Update peak to track the furthest point in the current direction
            if (sliderDirection === 'ArrowRight') {
                if (clientX > peakX) peakX = clientX;
            } else if (sliderDirection === 'ArrowLeft') {
                if (clientX < peakX) peakX = clientX;
            } else {
                // No direction yet — track both extremes via lastTurnX
                if (clientX > peakX) peakX = clientX;
                if (clientX < peakX && (peakX - clientX) > (clientX - lastTurnX)) {
                    // Reset: we haven't committed to a direction yet
                }
            }

            // Determine direction based on movement from turning point (or peak for reversals)
            let newDir = sliderDirection;
            if (sliderDirection === null) {
                // No direction yet — simple delta from initial touch
                if (delta > SLIDER_DEADZONE) {
                    newDir = 'ArrowRight';
                } else if (delta < -SLIDER_DEADZONE) {
                    newDir = 'ArrowLeft';
                }
            } else if (sliderDirection === 'ArrowRight') {
                // Currently going right — detect reversal from peak
                if (peakX - clientX > SLIDER_DEADZONE) {
                    newDir = 'ArrowLeft';
                }
            } else if (sliderDirection === 'ArrowLeft') {
                // Currently going left — detect reversal from peak
                if (clientX - peakX > SLIDER_DEADZONE) {
                    newDir = 'ArrowRight';
                }
            }

            // Apply styling
            if (newDir === 'ArrowLeft') {
                slider.className = 'touch-slider active-left';
            } else if (newDir === 'ArrowRight') {
                slider.className = 'touch-slider active-right';
            } else {
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
                // Reset peak to current position on direction change
                peakX = clientX;
            }
        };

        const resetSlider = () => {
            if (sliderDirection && this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnTouchKeyUp', sliderDirection);
            }
            sliderDirection = null;
            lastTurnX = null;
            peakX = null;
            sliderOriginX = null;
            slider.className = 'touch-slider';
            thumb.style.left = '50%';
        };

        // Touch events for slider
        slider.addEventListener('touchstart', (e) => {
            e.preventDefault();
            const x = e.touches[0].clientX;
            lastTurnX = x;
            peakX = x;
            sliderOriginX = x;
            thumb.style.left = '50%';
            slider.className = 'touch-slider active-center';
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
            const x = e.clientX;
            lastTurnX = x;
            peakX = x;
            sliderOriginX = x;
            thumb.style.left = '50%';
            slider.className = 'touch-slider active-center';
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
