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

        //if (!this.isMobile()) return;

        if (!this.canvas) {
            console.error('Canvas not found - cannot create touch controls');
            return;
        }

        // Create action buttons container (above canvas)
        const actionDiv = document.createElement('div');
        actionDiv.id = 'touch-controls-top';
        actionDiv.innerHTML = `
            <div class="touch-row">
                <button class="touch-btn touch-action" id="btn-1p">1P</button>
                <button class="touch-btn touch-action" id="btn-coin">COIN</button>
                <button class="touch-btn touch-action" id="btn-2p">2P</button>
            </div>
        `;

        // Create direction controls container (below canvas)
        const controlsDiv = document.createElement('div');
        controlsDiv.id = 'touch-controls';
        controlsDiv.innerHTML = `
            <div class="touch-row touch-row-direction">
                <div class="direction-controls" id="direction-controls">
                    <button class="touch-btn touch-dir" id="btn-left">&#9664;</button>
                    <button class="touch-btn touch-dir" id="btn-right">&#9654;</button>
                </div>
                <button class="touch-btn touch-fire" id="btn-fire">FIRE</button>
            </div>
        `;

        // Insert action buttons before the canvas wrapper, direction controls after
        const canvasWrapper = this.canvas.closest('.canvas-wrapper') || this.canvas.parentNode;
        const gameContainer = canvasWrapper.parentNode;
        gameContainer.insertBefore(actionDiv, canvasWrapper);
        gameContainer.insertBefore(controlsDiv, canvasWrapper.nextSibling);

        // --- Direction buttons logic (tap + slide-over) ---
        const dirContainer = document.getElementById('direction-controls');
        const btnLeft = document.getElementById('btn-left');
        const btnRight = document.getElementById('btn-right');
        let activeDir = null; // null, 'ArrowLeft', or 'ArrowRight'

        const hitTestDirection = (clientX, clientY) => {
            const leftRect = btnLeft.getBoundingClientRect();
            const rightRect = btnRight.getBoundingClientRect();
            if (clientX >= leftRect.left && clientX <= leftRect.right &&
                clientY >= leftRect.top && clientY <= leftRect.bottom) {
                return 'ArrowLeft';
            }
            if (clientX >= rightRect.left && clientX <= rightRect.right &&
                clientY >= rightRect.top && clientY <= rightRect.bottom) {
                return 'ArrowRight';
            }
            return null;
        };

        const setDirection = (newDir) => {
            if (newDir === activeDir) return;
            // Release previous
            if (activeDir) {
                (activeDir === 'ArrowLeft' ? btnLeft : btnRight).classList.remove('active');
                if (this.dotNetHelper) this.dotNetHelper.invokeMethodAsync('OnTouchKeyUp', activeDir);
            }
            // Press new
            if (newDir) {
                (newDir === 'ArrowLeft' ? btnLeft : btnRight).classList.add('active');
                if (this.dotNetHelper) this.dotNetHelper.invokeMethodAsync('OnTouchKeyDown', newDir);
            }
            activeDir = newDir;
        };

        const releaseDirection = () => {
            setDirection(null);
        };

        // Touch events on the direction container (handles slide-over)
        dirContainer.addEventListener('touchstart', (e) => {
            e.preventDefault();
            const t = e.touches[0];
            setDirection(hitTestDirection(t.clientX, t.clientY));
        }, { passive: false });

        dirContainer.addEventListener('touchmove', (e) => {
            e.preventDefault();
            const t = e.touches[0];
            setDirection(hitTestDirection(t.clientX, t.clientY));
        }, { passive: false });

        dirContainer.addEventListener('touchend', (e) => {
            e.preventDefault();
            releaseDirection();
        }, { passive: false });

        dirContainer.addEventListener('touchcancel', (e) => {
            e.preventDefault();
            releaseDirection();
        }, { passive: false });

        // Mouse events for desktop testing
        let dirMouseDown = false;
        dirContainer.addEventListener('mousedown', (e) => {
            e.preventDefault();
            dirMouseDown = true;
            setDirection(hitTestDirection(e.clientX, e.clientY));
        });
        document.addEventListener('mousemove', (e) => {
            if (dirMouseDown) {
                setDirection(hitTestDirection(e.clientX, e.clientY));
            }
        });
        document.addEventListener('mouseup', () => {
            if (dirMouseDown) {
                dirMouseDown = false;
                releaseDirection();
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
