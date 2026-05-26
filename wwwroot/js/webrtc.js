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

        // Called when we join voice. existingPeerIds is string[].
        // Throws if microphone access is denied (propagates to C# as JSException).
        async joinVoice(existingPeerIds) {
            _localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            for (const id of existingPeerIds) {
                await createOfferTo(id);
            }
        },

        // Called when a new peer joins (from existing participants' side).
        // Creates a peer connection ready to receive their offer.
        addPeer(connectionId) {
            if (!_peers.has(connectionId)) createPeerConnection(connectionId);
        },

        async handleOffer(fromConnectionId, sdp) {
            let pc = _peers.get(fromConnectionId);
            if (!pc) pc = createPeerConnection(fromConnectionId);
            await pc.setRemoteDescription({ type: 'offer', sdp });
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            await _dotNetRef.invokeMethodAsync('SendVoiceAnswer', fromConnectionId, answer.sdp);
        },

        async handleAnswer(fromConnectionId, sdp) {
            const pc = _peers.get(fromConnectionId);
            if (pc) await pc.setRemoteDescription({ type: 'answer', sdp });
        },

        async handleIceCandidate(fromConnectionId, candidateJson) {
            const pc = _peers.get(fromConnectionId);
            if (!pc || !candidateJson) return;
            try {
                await pc.addIceCandidate(JSON.parse(candidateJson));
            } catch (e) {
                // Benign: can happen if remote description isn't set yet; ignore
            }
        },

        // Toggle mute. Returns true if now muted.
        toggleMute() {
            if (!_localStream) return true;
            const tracks = _localStream.getAudioTracks();
            const nowEnabled = !tracks[0]?.enabled;
            tracks.forEach(t => { t.enabled = nowEnabled; });
            return !nowEnabled; // isMuted
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

    async function createOfferTo(connectionId) {
        const pc = createPeerConnection(connectionId);
        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        await _dotNetRef.invokeMethodAsync('SendVoiceOffer', connectionId, offer.sdp);
    }

    function createPeerConnection(connectionId) {
        const pc = new RTCPeerConnection({ iceServers: ICE_SERVERS });
        _peers.set(connectionId, pc);

        // Add our microphone so the remote peer can hear us
        if (_localStream) {
            _localStream.getTracks().forEach(t => pc.addTrack(t, _localStream));
        }

        pc.onicecandidate = (e) => {
            if (e.candidate) {
                _dotNetRef.invokeMethodAsync(
                    'SendVoiceIceCandidate', connectionId, JSON.stringify(e.candidate)
                );
            }
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

    function audioId(connectionId) {
        return 'va-' + connectionId.replace(/[^a-zA-Z0-9]/g, '_');
    }
})();
