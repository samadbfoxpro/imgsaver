
		document.getElementById('paste-area').addEventListener('paste', function(e) {
			e.preventDefault();

			const items = (e.clipboardData || e.originalEvent.clipboardData).items;
			let blob = null;

			for (let i = 0; i < items.length; i++) {
				if (items[i].type.indexOf('image') !== -1) {
					blob = items[i].getAsFile();
					break;
				}
			}

			if (blob) {
				const reader = new FileReader();
				reader.onload = function(event) {
					const img = document.createElement('img');
					img.src = event.target.result;
					img.style.maxWidth = '100%';
					img.style.maxHeight = '300px';
					img.style.borderRadius = '8px';
					img.style.boxShadow = '0 4px 10px rgba(0,0,0,0.3)';
					img.style.marginTop = '10px';

					const pasteArea = document.getElementById('paste-area');
					pasteArea.innerHTML = '';
					pasteArea.appendChild(img);

					document.getElementById('pasted_image_input').value = event.target.result;
				};
				reader.readAsDataURL(blob);
			} else {
				alert('عکسی در کلیپ‌بورد پیدا نشد! لطفاً یک عکس کپی کنید.');
			}
		});

		  document.querySelectorAll('.quick-btn').forEach(button => {
		button.addEventListener('click', function() {
			const textToAdd = this.getAttribute('data-text');
			const textarea = document.getElementById('negative_prompt');

			if (textarea.value.trim() !== '') {
				textarea.value += ', ';
			}

			textarea.value += textToAdd;
			textarea.focus();
		});
	});
