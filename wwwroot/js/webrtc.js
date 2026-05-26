'use strict';

(function () {
    const ICE_SERVERS = [
        { urls: 'stun:stun.l.google.com:19302' },
        { urls: 'stun:stun1.l.google.com:19302' },
    ];

    let _dotNetRef   = null;
    let _localStream = null;
    const _peers     = new Map(); // connectionId -> RTCPeerConnection

    window.voiceChat = {

        init(dotNetRef) {
            _dotNetRef = dotNetRef;
        },

        // Called when we join. existingPeerIds is a JS Array of strings.
        // Throws only if getUserMedia fails — offer creation is kicked off
        // asynchronously so the Blazor InvokeVoidAsync can return before
        // any .NET callbacks are made (avoids Blazor Server re-entrancy).
        async joinVoice(existingPeerIds) {
            if (!navigator.mediaDevices?.getUserMedia) {
                throw new Error('HTTPS_REQUIRED');
            }
            _localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });

            // Defer offer creation so this promise resolves first,
            // freeing the Blazor circuit before any .NET callbacks fire.
            if (existingPeerIds && existingPeerIds.length) {
                setTimeout(() => {
                    for (const id of existingPeerIds) {
                        createOfferTo(id).catch(console.error);
                    }
                }, 0);
            }
        },

        // Called when a new peer joins (from existing members' perspective).
        addPeer(connectionId) {
            if (!_peers.has(connectionId)) createPeerConnection(connectionId);
        },

        // C# calls this when we receive an offer. We create an answer and
        // notify .NET asynchronously so we don't block the return.
        async handleOffer(fromConnectionId, sdp) {
            let pc = _peers.get(fromConnectionId);
            if (!pc) pc = createPeerConnection(fromConnectionId);
            await pc.setRemoteDescription({ type: 'offer', sdp });
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            // Fire-and-forget — circuit is free by the time this resolves
            notify('SendVoiceAnswer', fromConnectionId, answer.sdp);
        },

        async handleAnswer(fromConnectionId, sdp) {
            const pc = _peers.get(fromConnectionId);
            if (pc) await pc.setRemoteDescription({ type: 'answer', sdp });
        },

        async handleIceCandidate(fromConnectionId, candidateJson) {
            const pc = _peers.get(fromConnectionId);
            if (!pc || !candidateJson) return;
            try { await pc.addIceCandidate(JSON.parse(candidateJson)); } catch (_) {}
        },

        // Returns true if now muted.
        toggleMute() {
            if (!_localStream) return true;
            const tracks = _localStream.getAudioTracks();
            const enable = !tracks[0]?.enabled;
            tracks.forEach(t => { t.enabled = enable; });
            return !enable;
        },

        removePeer(connectionId) {
            const pc = _peers.get(connectionId);
            if (pc) { pc.close(); _peers.delete(connectionId); }
            const el = document.getElementById(audioId(connectionId));
            if (el) el.remove();
        },

        leave() {
            _peers.forEach((pc, id) => {
                pc.close();
                const el = document.getElementById(audioId(id));
                if (el) el.remove();
            });
            _peers.clear();
            if (_localStream) {
                _localStream.getTracks().forEach(t => t.stop());
                _localStream = null;
            }
        },
    };

    // Fire-and-forget .NET notification — never awaited from JS so
    // we never block inside a Blazor-awaited function.
    function notify(method, ...args) {
        if (_dotNetRef) _dotNetRef.invokeMethodAsync(method, ...args).catch(console.error);
    }

    async function createOfferTo(connectionId) {
        const pc = createPeerConnection(connectionId);
        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        notify('SendVoiceOffer', connectionId, offer.sdp);
    }

    function createPeerConnection(connectionId) {
        const pc = new RTCPeerConnection({ iceServers: ICE_SERVERS });
        _peers.set(connectionId, pc);

        if (_localStream) {
            _localStream.getTracks().forEach(t => pc.addTrack(t, _localStream));
        }

        pc.onicecandidate = (e) => {
            if (e.candidate) notify('SendVoiceIceCandidate', connectionId, JSON.stringify(e.candidate));
        };

        pc.ontrack = (e) => {
            let el = document.getElementById(audioId(connectionId));
            if (!el) {
                el = document.createElement('audio');
                el.id = audioId(connectionId);
                el.autoplay = true;
                el.style.display = 'none';
                document.body.appendChild(el);
            }
            el.srcObject = e.streams[0];
        };

        return pc;
    }

    function audioId(id) {
        return 'va-' + id.replace(/[^a-zA-Z0-9]/g, '_');
    }
})();
