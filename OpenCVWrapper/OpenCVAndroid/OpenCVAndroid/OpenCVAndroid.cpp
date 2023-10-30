#include <opencv2/core.hpp>
#include <opencv2/calib3d.hpp>
#include <iostream>

bool SolvePnP(int npoints, float* objectPoints, float* imagePoints, float* cameraMatrix, float* distCoeffs, cv::OutputArray rvec, cv::OutputArray tvec) {
	cv::Mat objPoints = cv::Mat(npoints, 1, CV_32FC3, objectPoints);
	cv::Mat imgPoints = cv::Mat(npoints, 1, CV_32FC2, imagePoints);
	cv::Mat cm = cv::Mat(3, 3, CV_32FC1, cameraMatrix);
	cv::Mat dcs = cv::Mat(1, 8, CV_32FC1, distCoeffs);

	bool success = cv::solvePnP(objPoints, imgPoints, cm, dcs, rvec, tvec);

	objPoints.release();
	imgPoints.release();
	cm.release();
	dcs.release();

	return success;
}

void SetPoseArray(float* pose, cv::Mat rot, cv::Mat pos) {
	pose[0] = rot.at<float>(0, 0);
	pose[1] = rot.at<float>(0, 1);
	pose[2] = rot.at<float>(0, 2);
	pose[3] = rot.at<float>(1, 0);
	pose[4] = rot.at<float>(1, 1);
	pose[5] = rot.at<float>(1, 2);
	pose[6] = rot.at<float>(2, 0);
	pose[7] = rot.at<float>(2, 1);
	pose[8] = rot.at<float>(2, 2);

	pose[9] = pos.at<float>(0, 0);
	pose[10] = pos.at<float>(1, 0);
	pose[11] = pos.at<float>(2, 0);
}

extern "C" {
	float* CGetCameraPose(int npoints, float* objectPoints, float* imagePoints, float* cameraMatrix, float* distCoeffs) {
		cv::Mat rvec = cv::Mat(3, 1, CV_32FC1);
		cv::Mat tvec = cv::Mat(3, 1, CV_32FC1);;
		cv::Mat rot = cv::Mat(3, 3, CV_32FC1);;

		float* poseTmp = NULL;
		bool success = SolvePnP(npoints, objectPoints, imagePoints, cameraMatrix, distCoeffs, rvec, tvec);
		if (success == true) {
			poseTmp = new float[12];
			std::cout << "CGetCameraPose success, ALLOCATED memory at:" << poseTmp << std::endl;
			cv::Rodrigues(rvec, rot);
			cv::transpose(rot, rot);
			tvec = -rot * tvec;
			SetPoseArray(poseTmp, rot, tvec);
		}
		else {
			std::cout << "CGetCameraPose failed" << std::endl;
		}

		rvec.release();
		tvec.release();
		rot.release();

		return poseTmp;
	}

	void CFree(void* ptr) {
		if (ptr != NULL)
		{
			free(ptr);
			std::cout << "FREE memory at:" << ptr << std::endl;
		}
	}
}