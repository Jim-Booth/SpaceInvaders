// ============================================================================
// Project:     SpaceInvaders
// File:        game.js
// Description: Canvas rendering and audio interop for the Space Invaders
//              emulator, including touch control initialization for mobile
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// Notice:      Space Invaders is (c) 1978 Taito Corporation.
//              This emulator is for educational purposes only.
// ============================================================================

window.gameInterop = {
    canvas: null,
    ctx: null,
    imageData: null,
    audioCtx: null,
    audioBuffers: {},       // id -> AudioBuffer (decoded)
    loopingSources: {},     // id -> AudioBufferSourceNode (sustained sounds)
    dotNetHelper: null,
    _renderLoopRunning: false,
    _latestFrameReady: false,
    
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

        // Create shared AudioContext for Web Audio API
        this.audioCtx = new (window.AudioContext || window.webkitAudioContext)();

        console.log('Canvas initialized successfully');
        return true;
    },

    // Returns a promise that resolves after two browser paint frames, guaranteeing
    // the canvas is fully in the DOM before C# initialization begins.
    waitForPaint: function() {
        return new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    },

    // Receive a frame from C# and store it. The rAF render loop will draw it
    // at the next VSync, decoupling game logic timing from display timing.
    updateFrame: function(pixelData) {
        if (!this.imageData) return;
        this.imageData.data.set(new Uint8ClampedArray(pixelData));
        this._latestFrameReady = true;
    },

    // Start a requestAnimationFrame loop that flushes the latest frame to the
    // canvas at each display VSync. Runs independently of C# game logic.
    startRenderLoop: function() {
        this._renderLoopRunning = true;
        this._latestFrameReady = false;
        const loop = () => {
            if (!this._renderLoopRunning) return;
            if (this._latestFrameReady && this.ctx && this.imageData) {
                this.ctx.putImageData(this.imageData, 0, 0);
                this._latestFrameReady = false;
            }
            requestAnimationFrame(loop);
        };
        requestAnimationFrame(loop);
    },

    // Stop the render loop
    stopRenderLoop: function() {
        this._renderLoopRunning = false;
    },

    // Persist the high score to localStorage
    setHighScore: function(score) {
        localStorage.setItem('spaceInvadersHighScore', score.toString());
    },

    // Retrieve the persisted high score (returns 0 if never set)
    getHighScore: function() {
        const val = localStorage.getItem('spaceInvadersHighScore');
        return val ? parseInt(val, 10) : 0;
    },
    
    // Decode a sound file from bytes already fetched by the host (no duplicate network request)
    loadSoundFromBytes: async function(id, bytes) {
        try {
            // Copy out of WASM shared memory before handing to decodeAudioData
            const arrayBuffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
            const audioBuffer = await this.audioCtx.decodeAudioData(arrayBuffer);
            this.audioBuffers[id] = audioBuffer;
            console.log('Sound loaded:', id);
        } catch (e) {
            console.warn('Failed to load sound:', id, e);
        }
    },

    // Play a one-shot sound effect using the Web Audio API
    playSound: function(id) {
        const buffer = this.audioBuffers[id];
        if (!buffer || !this.audioCtx) return;
        if (this.audioCtx.state === 'suspended') this.audioCtx.resume();
        const source = this.audioCtx.createBufferSource();
        source.buffer = buffer;
        source.connect(this.audioCtx.destination);
        source.start();
    },

    // Start a sound looping continuously (e.g. UFO engine hum)
    startLoopingSound: function(id) {
        const buffer = this.audioBuffers[id];
        if (!buffer || !this.audioCtx) return;
        if (this.loopingSources[id]) return; // already looping
        if (this.audioCtx.state === 'suspended') this.audioCtx.resume();
        const source = this.audioCtx.createBufferSource();
        source.buffer = buffer;
        source.loop = true;
        source.connect(this.audioCtx.destination);
        source.start();
        this.loopingSources[id] = source;
    },

    // Stop a looping sound
    stopLoopingSound: function(id) {
        const source = this.loopingSources[id];
        if (source) {
            try { source.stop(); } catch (_) {}
            delete this.loopingSources[id];
        }
    },

    // Suspend all audio (e.g. when the game is paused)
    suspendAudio: function() {
        if (this.audioCtx) this.audioCtx.suspend();
    },

    // Resume all audio (e.g. when the game is unpaused)
    resumeAudio: function() {
        if (this.audioCtx) this.audioCtx.resume();
    },

    // Play multiple sounds in a single interop call (batched)
    playSounds: function(ids) {
        for (const id of ids) {
            this.playSound(id);
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
