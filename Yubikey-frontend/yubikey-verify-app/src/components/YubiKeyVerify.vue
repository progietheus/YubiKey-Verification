<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from "vue";
import axios from "axios";
import { useRoute } from "vue-router";

// --- State and Refs ---
const route = useRoute();
const passCode = ref("");
const showPassword = ref(false);
const message = ref("");
const userEmail = ref("");
const isSuccess = ref(false);
const isFormDisabled = ref(false);
const isLoading = ref(false);
const ellipsisStep = ref(0);
let ellipsisInterval: number | undefined;

// --- Lifecycle: On Mount ---
onMounted(async () => {
  // Check JTI status on load
  const jti = route.params.jti as string;
  try {
    const { data } = await axios.get(
      `https://localhost:7067/api/YubiKey/verify/status/${jti}`
    );
    if (!data.isValid) {
      if (data.message && data.message.toLowerCase().includes("succeeded")) {
        message.value = "This link has already been used.";
      } else {
        message.value = data.message || "This verification link has expired.";
      }
      isFormDisabled.value = true;
    }
    userEmail.value = data.email;
  } catch (err) {
    message.value = "This verification link is invalid or has expired.";
    isFormDisabled.value = true;
  }

  // Animate ellipsis when loading
  ellipsisInterval = window.setInterval(() => {
    if (isLoading.value) {
      ellipsisStep.value = (ellipsisStep.value + 1) % 3;
    } else {
      ellipsisStep.value = 0;
    }
  }, 400);
});

// --- Lifecycle: On Unmount ---
onBeforeUnmount(() => {
  if (ellipsisInterval) clearInterval(ellipsisInterval);
});

// --- Methods ---
const toggleVisibility = () => {
  showPassword.value = !showPassword.value;
};

const verifyYubiKey = async () => {
  message.value = "";
  isLoading.value = true;
  const jti = route.params.jti as string;
  const apiUrl = `https://localhost:7067/api/YubiKey/verify/${jti}`;
  try {
    const response = await axios.post(apiUrl, {
      passCode: passCode.value,
    });
    isSuccess.value = response.data.success;
    message.value = response.data.message;
    if (isSuccess.value) {
      passCode.value = "";
    }
  } catch (error) {
    isSuccess.value = false;
    if (axios.isAxiosError(error) && error.response) {
      message.value =
        error.response.data.message || "An error occurred during verification.";
    } else {
      message.value = "An unexpected error occurred.";
    }
    console.error(error);
  } finally {
    isLoading.value = false;
  }
};
isFormDisabled.value = false;
</script>

<template>
  <div class="flex flex-col w-full">
    <!-- Header -->
    <header class="w-full bg-white p-4 border-b border-gray-300 text-center">
      <div class="flex items-center justify-center gap-2">
        <span class="text-2xl text-[#777777]">Connecting to</span>
        <img
          src="/banner-key-icon.png"
          alt="Okta Admin Console"
          class="h-[26px]"
        />
      </div>
      <p class="text-sm text-[#777777]">
        Use your YubiKey to verify your identity
      </p>
    </header>

    <!-- Main Content -->
    <main class="flex justify-center">
      <div
        class="bg-white border border-gray-300 mt-24 min-w-[400px] max-h-[700px]"
      >
        <!-- Logo Section -->
        <div class="flex justify-center p-4 h-[150px] border-b border-gray-300">
          <img src="/okta-logo.png" alt="Okta Logo" class="h-[40px] mt-6" />
        </div>

        <div class="relative w-full max-w-[480px] p-8 pt-4">
          <!-- YubiKey Beacon -->
          <div
            class="absolute top-[-45px] left-1/2 -translate-x-1/2 h-[85px] w-[85px]"
          >
            <div
              class="absolute inset-0 bg-white rounded-full shadow-[0_0_0_15px_#fff]"
            >
              <div
                class="absolute inset-0 rounded-full bg-white bg-[url('/yubico_70x70.png')] bg-center bg-no-repeat bg-contain shadow-[0_0_0_15px_#fff]"
              >
                <div
                  class="absolute inset-[-5px] rounded-full border-2 border-[#a7a7a7]"
                ></div>
              </div>
            </div>
          </div>

          <!-- Form Section -->
          <form
            @submit.prevent="verifyYubiKey"
            class="w-full space-y-6 text-center mt-12"
          >
            <div class="space-y-2">
              <h1 class="text-[15px] font-semibold text-[#5e5e5e]">
                Verify with YubiKey
              </h1>
              <div class="flex items-center justify-center">
                <span class="inline-block align-middle mr-1">
                  <img src="/user-icon.svg" alt="" class="w-4 h-4" />
                </span>
                <span class="text-[13px] text-[#6b7280]">
                  {{ userEmail || "email@example.com" }}
                </span>
              </div>
            </div>

            <p class="text-[14px] text-[#6b7280]">
              Use your YubiKey to insert a verification code.
            </p>

            <!-- YubiKey Demo Image -->
            <div
              class="h-[102px] w-full bg-[url('/yubikeyDemo.png')] bg-center bg-no-repeat bg-contain"
            ></div>

            <div class="space-y-2">
              <p class="text-sm font-semibold text-left">
                Insert then tap your YubiKey
              </p>

              <!-- Input Field -->
              <div class="relative">
                <input
                  :type="showPassword ? 'text' : 'password'"
                  :disabled="isFormDisabled"
                  id="passCode"
                  v-model="passCode"
                  required
                  autocomplete="one-time-code"
                  placeholder="Touch YubiKey"
                  class="w-full px-3 py-3 pr-10 border border-[#d1d5db] focus:border-[#0052CC] focus:ring-1 focus:ring-[#0052CC] focus:outline-none transition-all duration-200"
                />
                <button
                  type="button"
                  @click="toggleVisibility"
                  :aria-label="showPassword ? 'Hide passcode' : 'Show passcode'"
                  class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 focus:outline-none"
                >
                  <svg
                    v-if="!showPassword"
                    class="h-5 w-5"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    viewBox="0 0 24 24"
                  >
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                    />
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      d="M2.458 12C3.732 7.943 7.522 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.478 0-8.268-2.943-9.542-7z"
                    />
                  </svg>
                  <svg
                    v-else
                    class="h-5 w-5"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    viewBox="0 0 24 24"
                  >
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.542-7a9.956 9.956 0 012.293-3.95m3.362-2.675A9.956 9.956 0 0112 5c4.478 0 8.268 2.943 9.542 7a9.973 9.973 0 01-4.043 5.306M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                    />
                    <path
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      d="M3 3l18 18"
                    />
                  </svg>
                </button>
              </div>
            </div>

            <button
              type="submit"
              :disabled="isFormDisabled || isLoading"
              class="w-full bg-[#0052CC] hover:bg-[#0747A6] text-white py-3 px-4 text-sm font-semibold transition-colors duration-200 focus:outline-none focus:ring-2 focus:ring-[#0052CC] focus:ring-offset-2 disabled:opacity-60 disabled:cursor-not-allowed flex items-center justify-center"
            >
              <span v-if="!isLoading">Verify</span>
              <span v-else class="flex items-center h-5">
                <span
                  class="inline-block w-1.5 h-1.5 mx-0.5 rounded-full bg-white animate-pulse"
                  :class="{ 'opacity-30': ellipsisStep !== 0 }"
                />
                <span
                  class="inline-block w-1.5 h-1.5 mx-0.5 rounded-full bg-white animate-pulse"
                  :class="{ 'opacity-30': ellipsisStep !== 1 }"
                />
                <span
                  class="inline-block w-1.5 h-1.5 mx-0.5 rounded-full bg-white animate-pulse"
                  :class="{ 'opacity-30': ellipsisStep !== 2 }"
                />
              </span>
            </button>
          </form>

          <!-- Message Display -->
          <div
            v-if="message"
            :class="[
              'mt-6 p-4 rounded-md border text-sm text-center',
              isSuccess
                ? 'bg-[#f0fdf4] border-[#bbf7d0] text-[#15803d]'
                : 'bg-[#fef2f2] border-[#fecaca] text-[#dc2626]',
            ]"
          >
            {{ message }}
          </div>
        </div>
      </div>
    </main>
  </div>
</template>
